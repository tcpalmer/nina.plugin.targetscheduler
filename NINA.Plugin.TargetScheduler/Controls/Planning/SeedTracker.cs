using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// Tracks the values a set of planning parameters were seeded with (e.g. from an existing target) so the UI
    /// can mark the fields that have since been changed.  Bind to a field's state with the indexer, for example
    /// {Binding Modified[MinimumTime]}.
    /// </summary>
    public class SeedTracker : INotifyPropertyChanged {
        private readonly Dictionary<string, (object seed, Func<object> current)> fields = new Dictionary<string, (object, Func<object>)>();

        public event PropertyChangedEventHandler PropertyChanged;

        public bool this[string name] => fields.TryGetValue(name, out var field) && !Equals(field.seed, field.current());

        public bool AnyModified => fields.Keys.Any(name => this[name]);

        public void Track(string name, Func<object> current) {
            fields[name] = (current(), current);
        }

        public void Clear() {
            fields.Clear();
            Refresh();
        }

        public void Refresh() {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnyModified)));
        }
    }
}
