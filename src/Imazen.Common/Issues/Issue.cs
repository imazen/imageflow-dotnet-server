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
    public class Issue : IIssue {
        public Issue() {
        }
        public Issue(string message) {
            Summary = message;
        }
        public Issue(string message, string details, IssueSeverity severity) {
            Summary = message;
            Details = details;
            Severity = severity;
        }
        public Issue(string message, IssueSeverity severity) {
            Summary = message;
            Severity = severity;
        }

        public Issue(string source, string message, string details, IssueSeverity severity) {
            Source = source;
            Summary = message;
            Details = details;
            Severity = severity;
        }

        public string Source { get; set; }

        public string Summary { get; set; } = null;

        public string Details { get; set; } = null;

        public IssueSeverity Severity { get; set; } = IssueSeverity.Warning;

        public override int GetHashCode() {
            StringBuilder sb = new StringBuilder(160);
            if (Source != null) sb.Append(Source);
            sb.Append('|');
            if (Summary != null) sb.Append(Summary);
            sb.Append('|');
            if (Details != null) sb.Append(Details);
            sb.Append('|');
            sb.Append((int)Severity);
            return sb.ToString().GetHashCode();
        }

        public override string ToString() {
            return Source + "(" + Severity.ToString() + "):\t" + Summary +
                    ("\n" + Details).Replace("\n", "\n\t\t\t") + "\n";
        }
        public override bool Equals(object obj) {
            if (obj == null) return base.Equals(obj);
            return GetHashCode() == obj.GetHashCode();
        }
    }
}
