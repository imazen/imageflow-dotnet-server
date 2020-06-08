// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the Apache License, Version 2.0.
﻿
using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.Common.Issues
{
    public class IssueSink: IIssueProvider,IIssueReceiver {
        private readonly string defaultSource = null;
        public IssueSink(string defaultSource) {
            this.defaultSource = defaultSource;
        }

        private readonly IDictionary<int, IIssue> issueSet = new Dictionary<int,IIssue>();
        private readonly IList<IIssue> issues = new List<IIssue>();
        private readonly object issueSync = new object();
        /// <summary>
        /// Returns a copy of the list of reported issues.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<IIssue> GetIssues() {
            lock (issueSync) {
                return new List<IIssue>(issues);
            }
        }  
        /// <summary>
        /// Adds the specified issue to the list unless it is an exact duplicate of another instance.
        /// </summary>
        /// <param name="i"></param>
        public virtual void AcceptIssue(IIssue i) {
            //Set default source value
            if (i.Source == null && i is Issue issue) issue.Source = defaultSource;

            //Perform duplicate checking, then add item if unique.
            var hash = i.Hash();
            lock (issueSync)
            {
                if (issueSet.ContainsKey(hash)) return;
                issueSet[hash] = i;
                issues.Add(i);
            }
        }
    }
}
