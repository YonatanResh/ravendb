// -----------------------------------------------------------------------
//  <copyright file="SubscriptionConnectionStatsScope.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using Raven.Client.Documents.Subscriptions;
using Raven.Server.Utils.Stats;

namespace Raven.Server.Documents.Subscriptions.Stats
{
    public class SubscriptionConnectionStatsScope : StatsScope<SubscriptionConnectionRunStats, SubscriptionConnectionStatsScope>
    {
        private readonly SubscriptionConnectionRunStats _stats;

        public SubscriptionConnectionStatsScope(SubscriptionConnectionRunStats stats, bool start = true) : base(stats, start)
        {
            _stats = stats;
        }

        protected override SubscriptionConnectionStatsScope OpenNewScope(SubscriptionConnectionRunStats stats, bool start)
        {
            return new SubscriptionConnectionStatsScope(stats, start);
        }

        public void RecordConnectionInfo(SubscriptionState subscriptionState, string clientUri)
        {
            _stats.TaskId = subscriptionState.SubscriptionId;
            _stats.TaskName = subscriptionState.SubscriptionName;
            _stats.Script = subscriptionState.Query;
            _stats.ClientUri = clientUri;
        }
        
        public void RecordException(string exception)
        {
            _stats.Exception = exception;
        }

        public void RecordBatchCompleted()
        {
            _stats.BatchCount++;
        }
    }
}