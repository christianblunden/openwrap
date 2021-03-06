﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenFileSystem.IO.FileSystem.InMemory;
using OpenRasta.Wrap.Tests.Dependencies.context;
using OpenWrap.Commands;
using OpenWrap.Commands.Wrap;
using OpenWrap.Configuration;
using OpenWrap.Configuration.remote_repositories;
using OpenWrap.Dependencies;
using OpenFileSystem.IO;
using OpenWrap.Repositories;
using OpenWrap.Services;
using OpenWrap.Testing;

namespace OpenWrap.Tests.Commands.context
{
    public abstract class command_context<T> : Testing.context
        where T : ICommand
    {
        protected AttributeBasedCommandDescriptor<T> Command;
        protected CommandRepository Commands;
        protected InMemoryEnvironment Environment;
        protected List<ICommandOutput> Results;
        InMemoryFileSystem FileSystem;

        public command_context()
        {
            Command = new AttributeBasedCommandDescriptor<T>();
            Commands = new CommandRepository { Command };

            WrapServices.Clear();
            var currentDirectory = System.Environment.CurrentDirectory;
            FileSystem = new InMemoryFileSystem() { CurrentDirectory = currentDirectory };
            Environment = new InMemoryEnvironment(
                FileSystem.GetDirectory(currentDirectory),
                FileSystem.GetDirectory(InstallationPaths.ConfigurationDirectory));
            WrapServices.RegisterService<IFileSystem>(FileSystem);
            WrapServices.RegisterService<IEnvironment>(Environment);
            WrapServices.RegisterService<IPackageManager>(new PackageManager());
            WrapServices.RegisterService<ICommandRepository>(Commands);
            WrapServices.RegisterService<IConfigurationManager>(new ConfigurationManager(Environment.ConfigurationDirectory));
        }

        protected void given_dependency(string dependency)
        {
            new DependsParser().Parse(dependency, Environment.Descriptor);
        }

        protected void given_file(string filePath, Stream stream)
        {

        }

        protected void given_project_package(string name, Version version, params string[] dependencies)
        {
            given_project_repository();
            AddPackage(Environment.ProjectRepository, name, version, dependencies);
        }

        protected void given_project_repository()
        {
            if (Environment.ProjectRepository == null)
                Environment.ProjectRepository = new InMemoryRepository("Project repository");
        }

        protected void given_remote_package(string name, Version version, params string[] dependencies)
        {
            AddPackage(Environment.RemoteRepository, name, version, dependencies);
        }

        protected void given_system_package(string name, Version version, params string[] dependencies)
        {
            AddPackage(Environment.SystemRepository, name, version, dependencies);
        }

        protected virtual void when_executing_command(params string[] parameters)
        {
            var allParams = new[] { Command.Noun, Command.Verb }.Concat(parameters);
            Results = new CommandLineProcessor(Commands).Execute(allParams).ToList();
        }

        protected static void AddPackage(InMemoryRepository repository, string name, Version version, string[] dependencies)
        {
            repository.Packages.Add(new InMemoryPackage
            {
                Name = name,
                Version = version,
                Source = repository,
                Dependencies = dependencies.SelectMany(x => DependsParser.ParseDependsInstruction(x).Dependencies)
                                           .ToList()
            });
        }

        protected void given_currentdirectory_package(string packageName, Version version, params string[] dependencies)
        {
            command_context<AddWrapCommand>.AddPackage(Environment.CurrentDirectoryRepository, packageName, version, dependencies);
        }

        protected void package_is_not_in_repository(InMemoryRepository repository, string packageName, Version packageVersion)
        {
            (repository.PackagesByName.Contains(packageName)
                              ? repository.PackagesByName[packageName].FirstOrDefault(x => x.Version.Equals(packageVersion))
                              : null).ShouldBeNull();


        }
        protected void package_is_in_repository(InMemoryRepository repository, string packageName, Version packageVersion)
        {
            repository.PackagesByName[packageName]
                .ShouldHaveCountOf(1)
                .First().Version.ShouldBe(packageVersion);
        }

        protected void given_remote_configuration(RemoteRepositories remoteRepositories)
        {
            WrapServices.GetService<IConfigurationManager>()
                    .Save(Configurations.Addresses.RemoteRepositories, remoteRepositories);
        }
    }
}