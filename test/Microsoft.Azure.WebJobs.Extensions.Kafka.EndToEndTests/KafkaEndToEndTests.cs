// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Kafka.EndToEndTests
{
    [Trait("Category", "E2E")]
    public class KafkaEndToEndTests
    {
        private readonly TestLoggerProvider loggerProvider;

        internal static TestLoggerProvider CreateTestLoggerProvider()
        {
            return (System.Diagnostics.Debugger.IsAttached) ?
                new TestLoggerProvider((l) => System.Diagnostics.Debug.WriteLine(l.ToString())) :
                new TestLoggerProvider();
        }

        public KafkaEndToEndTests()
        {
            this.loggerProvider = CreateTestLoggerProvider();
        }

        [Fact]
        public async Task StringValue_SingleTrigger_Resume_Continue_Where_Stopped()
        {
            const int producedMessagesCount = 80;
            var messageMasterPrefix = Guid.NewGuid().ToString();
            var messagePrefixBatch1 = messageMasterPrefix + ":1:";
            var messagePrefixBatch2 = messageMasterPrefix + ":2:";

            var loggerProvider1 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(SingleItemTrigger), typeof(KafkaOutputFunctions) }, loggerProvider1))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithOnePartition,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefixBatch1 + x));

                // await KafkaProducers.ProduceStringsAsync(Broker, StringTopicWithOnePartition, Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefixBatch1 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider1.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch1));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);
            }

            var loggerProvider2 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(SingleItemTrigger), typeof(KafkaOutputFunctions) }, loggerProvider2))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithOnePartition,
                    Enumerable.Range(1 + producedMessagesCount, producedMessagesCount).Select(x => messagePrefixBatch2 + x));
                    
                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider2.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch2));
                    return foundCount == producedMessagesCount;
                });
            }

            // Ensure 2 run does not have any item from previous run
            Assert.DoesNotContain(loggerProvider2.GetAllUserLogMessages().Where(p => p.FormattedMessage != null).Select(x => x.FormattedMessage), x => x.Contains(messagePrefixBatch1));
        }

        private MethodInfo GetStaticMethod(Type type, string methodName) => type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);

        [Fact]
        public async Task SinglePartition_StringValue_ArrayTrigger_Resume_Continue_Where_Stopped()
        {
            const int producedMessagesCount = 80;
            var messageMasterPrefix = Guid.NewGuid().ToString();
            var messagePrefixBatch1 = messageMasterPrefix + ":1:";
            var messagePrefixBatch2 = messageMasterPrefix + ":2:";

            var loggerProvider1 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(MultiItemTrigger), typeof(KafkaOutputFunctions) }, loggerProvider1))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithOnePartition,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefixBatch1 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider1.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch1));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);

                await host.StopAsync();
            }

            var loggerProvider2 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(MultiItemTrigger) }, loggerProvider2))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithOnePartition,
                    Enumerable.Range(1 + producedMessagesCount, producedMessagesCount).Select(x => messagePrefixBatch2 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider2.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch2));
                    return foundCount == producedMessagesCount;
                });

                await host.StopAsync();
            }

            // Ensure 2 run does not have any item from previous run
            Assert.DoesNotContain(loggerProvider2.GetAllUserLogMessages().Where(p => p.FormattedMessage != null).Select(x => x.FormattedMessage), x => x.Contains(messagePrefixBatch1));
        }

        [Fact]
        public async Task SinglePartition_StringValue_SingleTrigger_Resume_Continue_Where_Stopped()
        {
            const int producedMessagesCount = 80;
            var messageMasterPrefix = Guid.NewGuid().ToString();
            var messagePrefixBatch1 = messageMasterPrefix + ":1:";
            var messagePrefixBatch2 = messageMasterPrefix + ":2:";

            var loggerProvider1 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(SingleItemTriggerTenPartitions) }, loggerProvider1))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithTenPartitions,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefixBatch1 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider1.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch1));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);

                await host.StopAsync();
            }

            var loggerProvider2 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(SingleItemTriggerTenPartitions) }, loggerProvider2))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithTenPartitions,
                    Enumerable.Range(1 + producedMessagesCount, producedMessagesCount).Select(x => messagePrefixBatch2 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider2.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch2));
                    return foundCount == producedMessagesCount;
                });

                await host.StopAsync();
            }

            // Ensure 2 run does not have any item from previous run
            Assert.DoesNotContain(loggerProvider2.GetAllUserLogMessages().Where(p => p.FormattedMessage != null).Select(x => x.FormattedMessage), x => x.Contains(messagePrefixBatch1));
        }

        [Fact]
        public async Task MultiPartition_StringValue_ArrayTrigger_Resume_Continue_Where_Stopped()
        {
            const int producedMessagesCount = 80;
            var messageMasterPrefix = Guid.NewGuid().ToString();
            var messagePrefixBatch1 = messageMasterPrefix + ":1:";
            var messagePrefixBatch2 = messageMasterPrefix + ":2:";

            var loggerProvider1 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(MultiItemTriggerTenPartitions) }, loggerProvider1))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithTenPartitions,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefixBatch1 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider1.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch1));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);

                await host.StopAsync();
            }

            var loggerProvider2 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(MultiItemTriggerTenPartitions) }, loggerProvider2))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                    Constants.StringTopicWithTenPartitions,
                    Enumerable.Range(1 + producedMessagesCount, producedMessagesCount).Select(x => messagePrefixBatch2 + x));

                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider2.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefixBatch2));
                    return foundCount == producedMessagesCount;
                });

                await host.StopAsync();
            }

            // Ensure 2 run does not have any item from previous run
            Assert.DoesNotContain(loggerProvider2.GetAllUserLogMessages().Where(p => p.FormattedMessage != null).Select(x => x.FormattedMessage), x => x.Contains(messagePrefixBatch1));
        }

        /// <summary>
        /// Ensures that multiple hosts processing a topic with 10 partition share the content, having the events being processed at least once.
        /// </summary>
        [Fact]
        public async Task Multiple_Hosts_Process_Events_At_Least_Once()
        {
            const int producedMessagesCount = 240;
            var messagePrefix = Guid.NewGuid().ToString() + ":";

            var producerHost = await this.StartHostAsync(typeof(KafkaOutputFunctions));
            var producerJobHost = producerHost.GetJobHost();

            var producerTask = producerJobHost.CallOutputTriggerStringAsync(
                GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringTopic)),
                Constants.StringTopicWithTenPartitions,
                Enumerable.Range(1, producedMessagesCount).Select(x => EndToEndTestExtensions.CreateMessageValue(messagePrefix, x)), 
                TimeSpan.FromMilliseconds(100));

            IHost host1 = null, host2 = null;

            Func<LogMessage, bool> messageFilter = (LogMessage m) => m.FormattedMessage != null && m.FormattedMessage.Contains(messagePrefix);

            try
            {
                var host1Log = CreateTestLoggerProvider();
                var host2Log = CreateTestLoggerProvider();

                host1 = await StartHostAsync(typeof(MultiItemTriggerTenPartitions), host1Log);

                // wait until host1 receives partitions
                await TestHelpers.Await(() =>
                {
                    var host1HasPartitions = host1Log.GetAllLogMessages().Any(x => x.FormattedMessage != null && x.FormattedMessage.Contains("Assigned partitions"));
                    return host1HasPartitions;
                });


                host2 = await StartHostAsync(typeof(MultiItemTriggerTenPartitions), host2Log);

                // wait until partitions are distributed
                await TestHelpers.Await(() =>
                {
                    var host2HasPartitions = host2Log.GetAllLogMessages().Any(x => x.FormattedMessage != null && x.FormattedMessage.Contains("Assigned partitions"));
                    return host2HasPartitions;
                });

                await TestHelpers.Await(() =>
                {
                    var host1Events = host1Log.GetAllUserLogMessages().Where(messageFilter).Select(x => x.FormattedMessage).ToList();
                    var host2Events = host2Log.GetAllUserLogMessages().Where(messageFilter).Select(x => x.FormattedMessage).ToList();

                    return host1Events.Count > 0 &&
                        host2Events.Count > 0 &&
                        host2Events.Count + host1Events.Count >= producedMessagesCount;
                });


                await TestHelpers.Await(() =>
                {
                    // Ensure every message was processed at least once
                    var allLogs = new List<string>(host1Log.GetAllLogMessages().Where(messageFilter).Select(x => x.FormattedMessage));
                    allLogs.AddRange(host2Log.GetAllLogMessages().Where(messageFilter).Select(x => x.FormattedMessage));

                    for (int i = 1; i <= producedMessagesCount; i++)
                    {
                        var currentMessage = EndToEndTestExtensions.CreateMessageValue(messagePrefix, i);
                        var count = allLogs.Count(x => x == currentMessage);
                        if (count == 0)
                        {
                            return false;
                        }
                    }

                    return true;
                });

                // For history write down items that have more than once
                var logs = new List<string>(host1Log.GetAllLogMessages().Where(messageFilter).Select(x => x.FormattedMessage));
                logs.AddRange(host2Log.GetAllLogMessages().Where(messageFilter).Select(x => x.FormattedMessage));

                var multipleProcessItemCount = 0;
                for (int i = 1; i <= producedMessagesCount; i++)
                {
                    var currentMessage = EndToEndTestExtensions.CreateMessageValue(messagePrefix, i);
                    var count = logs.Count(x => x == currentMessage);
                    if (count > 1)
                    {
                        Assert.True(count < 3, "No item should be processed more than twice");
                        multipleProcessItemCount++;
                        Console.WriteLine($"{currentMessage} was processed {count} times");
                    }
                }

                // Should not process more than 10% of all items a second time.
                Assert.InRange(multipleProcessItemCount, 0, producedMessagesCount / 10);

            }
            finally
            {
                await host1?.StopAsync();
                await host2?.StopAsync();
            }

            await producerTask;
            await producerHost?.StopAsync();

        }

        [Fact]
        public async Task Produce_And_Consume_With_Key_OfType_Long()
        {
            const int producedMessagesCount = 80;
            var messagePrefix = Guid.NewGuid().ToString() + ":";

            var loggerProvider1 = CreateTestLoggerProvider();

            using (var host = await StartHostAsync(new[] { typeof(StringTopicWithLongKeyAndTenPartitionsTrigger), typeof(KafkaOutputFunctions) }, loggerProvider1))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringWithLongKeyAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendToStringWithLongKeyTopic)),
                    Constants.StringTopicWithLongKeyAndTenPartitions,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefix + x),
                    Enumerable.Range(1, producedMessagesCount).Select(x => x % 20L));
                    
                await TestHelpers.Await(() =>
                {
                    var foundCount = loggerProvider1.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefix));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);
            }
        }

        [Fact]
        public async Task Produce_And_Consume_Specific_Avro()
        {
            const int producedMessagesCount = 80;
            var messagePrefix = Guid.NewGuid().ToString() + ":";

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(MyRecordAvroTrigger) }))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringWithStringKeyAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendAvroWithStringKeyTopic)),
                    Constants.MyAvroRecordTopic,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefix + x),
                    Enumerable.Range(1, producedMessagesCount).Select(x => "record_" + (x % 20).ToString())
                    );

                await TestHelpers.Await(() =>
                {
                    var foundCount = this.loggerProvider.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefix));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);
            }
        }

        [Fact]
        public async Task Produce_And_Consume_Protobuf()
        {
            const int producedMessagesCount = 80;
            var messagePrefix = Guid.NewGuid().ToString() + ":";

            using (var host = await StartHostAsync(new[] { typeof(KafkaOutputFunctions), typeof(MyProtobufTrigger) }))
            {
                var jobHost = host.GetJobHost();

                await jobHost.CallOutputTriggerStringWithStringKeyAsync(
                    GetStaticMethod(typeof(KafkaOutputFunctions), nameof(KafkaOutputFunctions.SendProtobufWithStringKeyTopic)),
                    Constants.MyProtobufTopic,
                    Enumerable.Range(1, producedMessagesCount).Select(x => messagePrefix + x),
                    Enumerable.Range(1, producedMessagesCount).Select(x => "record_" + (x % 20).ToString())
                    );

                await TestHelpers.Await(() =>
                {
                    var foundCount = this.loggerProvider.GetAllUserLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains(messagePrefix));
                    return foundCount == producedMessagesCount;
                });

                // Give time for the commit to be saved
                await Task.Delay(1500);
            }
        }


        private Task<IHost> StartHostAsync(Type testType, ILoggerProvider customLoggerProvider = null) => StartHostAsync(new[] { testType }, customLoggerProvider);

        private async Task<IHost> StartHostAsync(Type[] testTypes, ILoggerProvider customLoggerProvider = null)
        {
            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder
                    .AddAzureStorage()
                    .AddKafka();
                })
                .ConfigureAppConfiguration(c =>
                {
                    //c.AddTestSettings();
                    //c.AddJsonFile("appsettings.tests.json", optional: false);
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITypeLocator>(new ExplicitTypeLocator(testTypes));
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(customLoggerProvider ?? this.loggerProvider);
                })
                .Build();

            await host.StartAsync();
            return host;
        }
    }
}