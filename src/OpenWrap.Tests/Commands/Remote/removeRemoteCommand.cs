﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenWrap.Configuration;
using OpenWrap.Services;
using OpenWrap.Testing;
using OpenWrap.Commands.Remote;
using OpenWrap.Configuration.remote_repositories;

namespace OpenWrap.Tests.Commands.Remote.Remove
{
    public class when_removing_a_non_existing_command : context.command_context<RemoveRemoteCommand>
    {
        public when_removing_a_non_existing_command()
        {
            given_remote_configuration(new RemoteRepositories());
            when_executing_command("esgaroth");
        }
        [Test]
        public void an_error_is_returned()
        {
            Results.ShouldHaveAtLeastOne(x => x.Success == false);
        }
    }
    public class when_removing_an_existing_command
        : context.command_context<RemoveRemoteCommand>
    {
        public when_removing_an_existing_command()
        {
            given_remote_configuration(new RemoteRepositories{{"esgaroth",null}});
            when_executing_command("esgaroth");
        }
        [Test]
        public void the_remote_repository_is_removed()
        {
            WrapServices.GetService<IConfigurationManager>()
                    .LoadRemoteRepositories()
                    .ContainsKey("esgaroth")
                    .ShouldBeFalse();
        }
    }
}
