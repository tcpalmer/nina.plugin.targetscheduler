using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Sequencer.Logic;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace NINA.Plugin.TargetScheduler.Symbol {

    public sealed class SymbolPublisher : IDisposable {
        private static readonly Lazy<SymbolPublisher> lazy = new Lazy<SymbolPublisher>(() => new SymbolPublisher());
        public static SymbolPublisher Instance { get => lazy.Value; }

        public const string SYMBOL_VERSION = "TS_Version";
        public const string SYMBOL_CONTAINER_RUNNING = "TS_ContainerRunning";
        public const string SYMBOL_CONTAINER_WAITING = "TS_ContainerWaiting";
        public const string SYMBOL_CONTAINER_PAUSED = "TS_ContainerPaused";
        public const string SYMBOL_CONTAINER_LAST_STARTED = "TS_ContainerLastStarted";
        public const string SYMBOL_CONTAINER_LAST_STOPPED = "TS_ContainerLastStopped";
        public const string SYMBOL_CURRENT_TARGET_NAME = "TS_CurrentTargetName";
        public const string SYMBOL_CURRENT_PROJECT_NAME = "TS_CurrentProjectName";
        public const string SYMBOL_CURRENT_TARGET_COORDINATES = "TS_CurrentTargetCoordinates";
        public const string SYMBOL_CURRENT_TARGET_ROTATION = "TS_CurrentTargetRotation";
        public const string SYMBOL_CURRENT_TARGET_STARTED = "TS_CurrentTargetStarted";
        public const string SYMBOL_FILTER_NAME = "TS_CurrentFilterName";
        public const string SYMBOL_EXPOSURE_LENGTH = "TS_CurrentExposureLength";
        public const string SYMBOL_NEXT_TARGET_START = "TS_NextTargetStart";
        public const string SYMBOL_NEXT_TARGET_NAME = "TS_NextTargetName";
        public const string SYMBOL_NEXT_PROJECT_NAME = "TS_NextProjectName";

        private static readonly List<string> _tokensList = new() {
            SYMBOL_VERSION,
            SYMBOL_CONTAINER_RUNNING, // bool
            SYMBOL_CONTAINER_WAITING, // bool
            SYMBOL_CONTAINER_PAUSED, // bool
            SYMBOL_CONTAINER_LAST_STARTED, // DateTime
            SYMBOL_CONTAINER_LAST_STOPPED, // DateTime
            SYMBOL_CURRENT_TARGET_NAME, // string
            SYMBOL_CURRENT_PROJECT_NAME, // string
            SYMBOL_CURRENT_TARGET_COORDINATES, // NINA Coordinates
            SYMBOL_CURRENT_TARGET_ROTATION, // double
            SYMBOL_CURRENT_TARGET_STARTED, // DateTime
            SYMBOL_FILTER_NAME, // string
            SYMBOL_EXPOSURE_LENGTH, // double
            SYMBOL_NEXT_TARGET_START, // DateTime
            SYMBOL_NEXT_TARGET_NAME, // string
            SYMBOL_NEXT_PROJECT_NAME };// string

        private static readonly ImmutableList<string> _tokens = _tokensList.ToImmutableList();

        private static readonly List<string> _tokensRetainList = new List<string> {
            SYMBOL_VERSION,
            SYMBOL_CONTAINER_LAST_STARTED,
            SYMBOL_CONTAINER_LAST_STOPPED,
        };

        private static readonly ImmutableList<string> _tokensRetain = _tokensRetainList.ToImmutableList();

        private ISymbolBroker _broker;
        private ISymbolProvider _provider;

        public SymbolPublisher() {
        }

        public void Init(ISymbolBroker broker) {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _provider = _broker.RegisterSymbolProvider("TargetScheduler");

            foreach (var name in _tokens) AddOrUpdate(name, null);
            _ = new SymbolEventHandler(TargetScheduler.EventMediator, this);

            TSLogger.Debug("symbol handling initialized");
        }

        public void AddOrUpdate(string name, object value) {
            _provider.AddOrUpdateSymbol(name, value);
            if (!_tokens.Contains(name)) _tokens.Add(name);
            TSLogger.Debug($"added/updated symbol {name}={value}");
        }

        public object GetValue(string name) {
            if (_broker.TryGetValue(name, out var value))
                return value;
            else return null;
        }

        public void LogAllValues() {
            foreach (var name in _tokens)
                TSLogger.Debug($"symbol {name}={GetValue(name)}");
        }

        public void Reset() {
            foreach (var name in _tokens) {
                if (!_tokensRetain.Contains(name)) AddOrUpdate(name, null);
            }

            TSLogger.Debug("symbols reset");
        }

        public void Dispose() {
            foreach (var name in _tokens) _provider.RemoveSymbol(name);
        }
    }
}