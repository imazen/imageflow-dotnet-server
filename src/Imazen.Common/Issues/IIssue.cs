// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.Common.Issues {
    public interface IIssue {
        string Source { get; }
        string Summary { get; }
        string Details { get; }
        IssueSeverity Severity { get; }
        int Hash();
    }
}
