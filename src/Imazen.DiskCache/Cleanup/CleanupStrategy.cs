/* Copyright (c) 2014 Imazen See license.txt for your rights. */

using System;
using System.Collections.Generic;
using System.Text;
using Imazen.Common.Issues;
using Imazen.DiskCache.Index;

namespace Imazen.DiskCache.Cleanup {
    public class CleanupStrategy :IssueSink
    {

        public CleanupStrategy() : base("DiskCache.CleanupStrategy")
        {
            SaveDefaults();
        }


        private readonly string[] properties = new string[] {
            "StartupDelay", "MinDelay", "MaxDelay", "OptimalWorkSegmentLength", "AvoidRemovalIfUsedWithin", "AvoidRemovalIfCreatedWithin"
            , "ProhibitRemovalIfUsedWithin", "ProhibitRemovalIfCreatedWithin", "TargetItemsPerFolder", "MaximumItemsPerFolder"};
        
        
        private readonly Dictionary<string,object> defaults = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Saves the current settings to the dictionary of default settings.
        /// </summary>
        private void SaveDefaults(){
            var t = this.GetType();
            foreach(var s in properties)
            {
                var pi = t.GetProperty(s);
                // ReSharper disable once PossibleNullReferenceException
                defaults[s] = pi.GetValue(this, null);
            }
        }
        /// <summary>
        /// Restores the default property values
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void RestoreDefaults() {
            var t = GetType();
            foreach(var s in properties)
            {
                var pi = t.GetProperty(s);
                // ReSharper disable once PossibleNullReferenceException
                pi.SetValue(this, defaults[s], null);
            }
        }

        /// <summary>
        /// How long to wait before beginning the initial cache indexing and cleanup.
        /// </summary>
        public TimeSpan StartupDelay { get; set; } = new TimeSpan(0, 5, 0);

        /// <summary>
        /// The minimum amount of time to wait after the most recent BeLazy to begin working again.
        /// </summary>
        public TimeSpan MinDelay { get; set; } = new TimeSpan(0, 0, 20);

        /// <summary>
        /// The maximum amount of time to wait between work segments
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = new TimeSpan(0, 5, 0);

        /// <summary>
        /// The optimal length for a work segment. Not always achieved.
        /// </summary>
        public TimeSpan OptimalWorkSegmentLength { get; set; } = new TimeSpan(0, 0, 4);


        /// <summary>
        /// The ideal number of cached files per folder. (defaults to 400) Only reached if it can be achieved without violating the AvoidRemoval... limits
        /// </summary>
        public int TargetItemsPerFolder { get; set; } = 400;

        /// <summary>
        /// The maximum number of cached files per folder. (defaults to 1000) Only reached if it can be achieved without violating the ProhibitRemoval... limits
        /// </summary>
        public int MaximumItemsPerFolder { get; set; } = 1000;


        /// <summary>
        /// Please note "LastUsed" values are (initially) only accurate to about a hour, due to delayed write. 
        /// If a file has been used after the app started running, the last used date is accurate.
        /// </summary>
        public TimeSpan AvoidRemovalIfUsedWithin { get; set; } = new TimeSpan(96,0,0);

        public TimeSpan AvoidRemovalIfCreatedWithin { get; set; } = new TimeSpan(24,0,0);

        /// <summary>
        /// Please note "LastUsed" values are (initially) only accurate to about a hour, due to delayed write. 
        /// If a file has been used after the app started running, the last used date is accurate.
        /// </summary>
        public TimeSpan ProhibitRemovalIfUsedWithin { get; set; } = new TimeSpan(0,5,0);

        public TimeSpan ProhibitRemovalIfCreatedWithin { get; set; } = new TimeSpan(0,10,0);


        internal bool MeetsCleanupCriteria(CachedFileInfo i) {
            DateTime now = DateTime.UtcNow;
            //Only require the 'used' date to comply if it 1) doesn't match created date and 2) is above 0
            return ((now.Subtract(i.AccessedUtc) > AvoidRemovalIfUsedWithin || AvoidRemovalIfUsedWithin <= new TimeSpan(0) || i.AccessedUtc == i.UpdatedUtc) &&
                (now.Subtract(i.UpdatedUtc) > AvoidRemovalIfCreatedWithin || AvoidRemovalIfCreatedWithin <= new TimeSpan(0)));
        }

        internal bool MeetsOverMaxCriteria(CachedFileInfo i) {
            DateTime now = DateTime.UtcNow;
            //Only require the 'used' date to comply if it 1) doesn't match created date and 2) is above 0
            return ((now.Subtract(i.AccessedUtc) > ProhibitRemovalIfUsedWithin || ProhibitRemovalIfUsedWithin <= new TimeSpan(0) || i.AccessedUtc == i.UpdatedUtc) &&
                (now.Subtract(i.UpdatedUtc) > ProhibitRemovalIfCreatedWithin || ProhibitRemovalIfCreatedWithin <= new TimeSpan(0)));
        }

        internal bool ShouldRemove(string relativePath, CachedFileInfo info, bool isOverMax)
        {
            return isOverMax ? MeetsOverMaxCriteria(info) : MeetsCleanupCriteria(info);
        }


        public override IEnumerable<IIssue> GetIssues() {
            var issues = new List<IIssue>(base.GetIssues());
            var t = this.GetType();
            var sb = new StringBuilder();
            foreach(var s in defaults.Keys){
                var pi = t.GetProperty(s);
                // ReSharper disable once PossibleNullReferenceException
                var v = pi.GetValue(this,null);
                if (!v.Equals(defaults[s]))
                    sb.AppendLine(s + " has been changed to " + v + " instead of the suggested value, " + defaults[s]);
            }
            if (sb.Length > 0)
                issues.Add(new Issue( "The cleanup strategy settings have been changed. This is not advised, and may have ill effects. " +
                "\nThe default settings for the cleanup strategy were carefully chosen, and should not be changed except at the suggestion of the support.\n" + sb, IssueSeverity.Warning));

            return issues;
        }
    }
}
