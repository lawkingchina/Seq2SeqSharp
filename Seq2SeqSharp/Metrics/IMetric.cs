﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seq2SeqSharp.Metrics
{
    public interface IMetric
    {
        void Evaluate(List<List<string>> refTokens, List<string> hypTokens);
        string GetScoreStr();
        string Name { get; }
        double GetPrimaryScore();
        void ClearStatus();

    }
}
