﻿#region
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenFileSystem.IO;

#endregion

namespace OpenWrap.Configuration
{
    public class ConfigurationManager : IConfigurationManager
    {
        readonly IDirectory _configurationDirectory;

        public ConfigurationManager(IDirectory configurationDirectory)
        {
            _configurationDirectory = configurationDirectory;
            BaseUri = Configurations.Addresses.BaseUri;
        }

        public Uri BaseUri { get; private set; }

        public T Load<T>(Uri uri) where T : new()
        {
            var file = GetConfigurationFile(uri);
            if (!file.Exists)
                return GetDefaultValueFor<T>();
            return ParseFile<T>(file);
        }

        IFile GetConfigurationFile(Uri uri)
        {
            return _configurationDirectory.GetFile(Configurations.Addresses.BaseUri.MakeRelativeUri(uri).ToString());
        }

        T GetDefaultValueFor<T>()
        {
            var pi = typeof(T).GetProperty("Default", BindingFlags.Static | BindingFlags.Public);
            var fi = typeof(T).GetField("Default", BindingFlags.Static | BindingFlags.Public);
            if (pi == null)
                if (fi == null)
                    return default(T);
                else 
                    return (T)fi.GetValue(null);

            return (T)pi.GetValue(null, null);
        }

        public void Save<T>(Uri uri, T configEntry)
        {
            var configFile = GetConfigurationFile(uri);
            using (var writer = new StreamWriter(configFile.OpenWrite()))
            {
                WriteProperties(writer, configEntry);

                var dictionaryInterface = FindDictionaryInterface<T>();
                if (dictionaryInterface != null)
                {
                    WriteDictionaryEntries(writer, dictionaryInterface, configEntry);
                }
            }
        }

        void WriteDictionaryEntries<T>(StreamWriter configFile, Type dictionaryInterface, T configEntry)
        {
            
            var entryType = dictionaryInterface.GetGenericArguments()[1];
            var kvPairType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), entryType);
            var kvKey = kvPairType.GetProperty("Key");
            var kvValue = kvPairType.GetProperty("Value");

            var kvInterface = typeof(IEnumerable<>).MakeGenericType(kvPairType);

            var enumerator =(IEnumerator) kvInterface.GetMethod("GetEnumerator").Invoke(configEntry, new object[0]);
            Func<object, string> keyReader = x => (string)kvKey.GetValue(x, null);
            Func<object, object> valueReader = x => kvValue.GetValue(x, null);

            while (enumerator.MoveNext())
            {
                var key = keyReader(enumerator.Current);
                var value = valueReader(enumerator.Current);

                WriteSection(configFile, entryType.Name, key, value);
            }

        }

        void WriteSection(StreamWriter configFile, string sectionName, string key, object value)
        {
            configFile.WriteSection(sectionName, key);
            WriteProperties(configFile, value);
        }

        void WriteProperties(StreamWriter configFile, object value)
        {
            if (value == null)
                return;
            var properties = from pi in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             where pi.GetIndexParameters().Length == 0
                             let propertyValue = pi.GetValue(value, null)
                             where propertyValue != null
                             select new { pi, propertyValue };

            foreach (var property in properties)
                configFile.WriteProperty(property.pi.Name.ToLowerInvariant(), property.propertyValue);
        }

        static T ParseFile<T>(IFile file) where T : new()
        {
            string data;
            using (var fileStream = file.OpenRead())
                data = Encoding.UTF8.GetString(fileStream.ReadToEnd());

            var parsedConfig = new ConfigurationParser().Parse(data);
            var configData = new T();

            var dictionaryInterface = FindDictionaryInterface<T>();
            if (dictionaryInterface != null)
            {
                PopulateDictionaryEntries(file, dictionaryInterface, parsedConfig, configData);
            }
            return configData;
        }

        static object AssignPropertiesFromLines(object instance, IEnumerable<ConfigurationLine> lines)
        {
            var type = instance.GetType();

            foreach (var line in lines)
            {
                var property = type.GetProperty(line.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property == null)
                    continue;
                var propertyValue = property.PropertyType.CreateInstanceFrom(line.Value);

                property.SetValue(instance, propertyValue, null);
            }
            return instance;
        }

        static void PopulateDictionaryEntries<T>(IFile file, Type dictionaryInterface, IEnumerable<ConfigurationEntry> parsedConfig, T configData)
        {
            var dictionaryParameterType = dictionaryInterface.GetGenericArguments()[1];
            var addMethod = dictionaryInterface.GetMethod("Add", new[] { typeof(string), dictionaryParameterType });

            foreach (var section in parsedConfig.OfType<ConfigurationSection>().Where(x => x.Type.Equals(dictionaryParameterType.Name, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    addMethod.Invoke(configData, new[] { section.Name, AssignPropertiesFromLines(Activator.CreateInstance(dictionaryParameterType), section.Lines) });
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException is ArgumentException)
                        throw new InvalidConfigurationException(
                                string.Format("Duplicate configuration section of type '{0}' with name '{1} in the file '{2}' found. Correct the issue and retry.",
                                              section.Type,
                                              section.Name,
                                              file.Path.FullPath),
                                e.InnerException);
                    else
                        throw;
                }
            }
        }

        static Type FindDictionaryInterface<T>()
        {
            return typeof(T).GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>) && x.GetGenericArguments()[0] == typeof(string));
        }
    }
    public static class StreamWriterExtensions
    {
        public static void WriteSection(this StreamWriter writer, string sectionType, string sectionName)
        {

            writer.WriteLine();
            if (string.IsNullOrEmpty(sectionName))
                writer.WriteLine("[{0}]", sectionType.ToLower());
            else
                writer.WriteLine("[{0} {1}]", sectionType.ToLower(), sectionName);
        }
        public static void WriteProperty(this StreamWriter writer, string name, object value)
        {
            writer.WriteLine("{0} = {1}", name, value);
        }
    }
}