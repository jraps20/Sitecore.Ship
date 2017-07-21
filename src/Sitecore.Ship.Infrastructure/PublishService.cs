using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.Publish;
using Sitecore.Ship.Core.Contracts;
using Sitecore.Ship.Core.Domain;

namespace Sitecore.Ship.Infrastructure
{
    public class PublishService : IPublishService
    {
        private readonly Dictionary<string, Func<Database, Database[], Language[], Handle>> _publishingActions;

        public PublishService()
        {
            _publishingActions = new Dictionary<string, Func<Database, Database[], Language[], Handle>>
                {
                    { "full",           PublishManager.Republish },
                    { "smart",          PublishManager.PublishSmart },
                    { "incremental",    PublishManager.PublishIncremental }
                };
        }

        public void Run(ItemsToPublish itemsToPublish)
        {
            if (itemsToPublish == null)
            {
                throw new ArgumentNullException("itemsToPublish");
            }

            if (itemsToPublish.Items.Count == 0)
            {
                return;
            }
            
            using (new SecurityModel.SecurityDisabler())
            {
                var master = Sitecore.Configuration.Factory.GetDatabase("master");

                foreach (var targetDatabase in itemsToPublish.TargetDatabases.Select(Sitecore.Configuration.Factory.GetDatabase))
                {
                    foreach (var language in itemsToPublish.TargetLanguages.Select(LanguageManager.GetLanguage))
                    {
                        var publishOptions = new PublishOptions(
                            master,
                            targetDatabase,
                            PublishMode.Full,
                            language,
                            DateTime.Now)
                        {
                            CompareRevisions = false, // default to false for list of items
                            Deep = false // don't publish children for list of items
                        };

                        var context = PublishManager.CreatePublishContext(publishOptions);

                        context.Languages = itemsToPublish.TargetLanguages.Select(LanguageManager.GetLanguage);

                        var publishingCandidateList = (
                            from itemIdToPublish
                            in itemsToPublish.Items
                                .Select(i => new ID(i))
                            let item = master.GetItem(itemIdToPublish)
                            where item != null
                            select new PublishingCandidate(itemIdToPublish, "*", publishOptions));

                        context.Queue.Add(publishingCandidateList);

                        var queue = new ProcessQueue();

                        queue.Process(context);
                    }
                }
            }
        }

        public void Run(PublishParameters publishParameters)
        {
            var publishingMode = publishParameters.Mode.ToLower();

            if (!_publishingActions.ContainsKey(publishingMode))
            {
                throw new InvalidOperationException($"Invalid publishing mode ({publishingMode})");
            }

            PublishingTask(_publishingActions[publishingMode], publishParameters);
        }

        public DateTime GetLastCompletedRun(PublishLastCompleted completeParameters)
        {
            // please note http://stackoverflow.com/questions/12416141/get-the-date-time-that-sitecore-last-published

            var source = Sitecore.Configuration.Factory.GetDatabase(completeParameters.Source);
            var target = Sitecore.Configuration.Factory.GetDatabase(completeParameters.Target);

            var language = LanguageManager.GetLanguage(completeParameters.Language);


            Assert.IsNotNull(source, "Source database {0} cannot be found".Formatted(completeParameters.Source));
            Assert.IsNotNull(source, "Target database {0} cannot be found".Formatted(completeParameters.Target));
            Assert.IsNotNull(language, "Language {0} cannot be found".Formatted(completeParameters.Language));

            var date = source.Properties.GetLastPublishDate(target, language);
            return date;
        }

        private static void PublishingTask(Func<Database, Database[], Language[], Handle> publishType, PublishParameters publishParameters)
        {
            using (new SecurityModel.SecurityDisabler())
            {
                var master = Sitecore.Configuration.Factory.GetDatabase(publishParameters.Source);
                var targetDBs = publishParameters.Targets.Select(Sitecore.Configuration.Factory.GetDatabase).ToArray();
                var languages = publishParameters.Languages.Select(LanguageManager.GetLanguage).ToArray();

                publishType(master, targetDBs, languages);
            }
        }
    }
}