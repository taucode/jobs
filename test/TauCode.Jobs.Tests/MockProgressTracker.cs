using System;
using System.Collections.Generic;

namespace TauCode.Jobs.Tests
{
    internal class MockProgressTracker : IProgressTracker
    {
        internal List<decimal> _list = new List<decimal>();

        public void UpdateProgress(decimal? percentCompleted, DateTimeOffset? estimatedEndTime)
        {
            _list.Add(percentCompleted ?? throw new ArgumentNullException());
        }

        internal IReadOnlyList<decimal> GetList() => _list;
    }
}
