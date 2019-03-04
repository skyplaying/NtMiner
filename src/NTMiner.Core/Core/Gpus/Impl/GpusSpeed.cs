﻿using NTMiner.Core.Impl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NTMiner.Core.Gpus.Impl {
    internal class GpusSpeed : IGpusSpeed {
        private readonly Dictionary<int, GpuSpeed> _currentGpuSpeed = new Dictionary<int, GpuSpeed>();
        private Dictionary<int, List<IGpuSpeed>> _gpuSpeedHistory = new Dictionary<int, List<IGpuSpeed>>();

        private readonly INTMinerRoot _root;
        public GpusSpeed(INTMinerRoot root) {
            _root = root;
            DateTime now = DateTime.Now;
            foreach (var gpu in _root.GpuSet) {
                _currentGpuSpeed.Add(gpu.Index, new GpuSpeed(gpu, new Speed() {
                    Value = 0,
                    SpeedOn = now
                }, new Speed() {
                    Value = 0,
                    SpeedOn = now
                }));
            }
            GpuSpeed totalGpuSpeed = this._currentGpuSpeed[NTMinerRoot.GpuAllId];
            var speedExceptTotal = _currentGpuSpeed.Values.Where(a => a != totalGpuSpeed).ToArray();
            totalGpuSpeed.UpdateMainCoinSpeed(speedExceptTotal.Sum(a => a.MainCoinSpeed.Value), now);
            totalGpuSpeed.UpdateDualCoinSpeed(speedExceptTotal.Sum(a => a.DualCoinSpeed.Value), now);
            foreach (var item in _currentGpuSpeed) {
                _gpuSpeedHistory.Add(item.Key, new List<IGpuSpeed>());
            }
            VirtualRoot.On<Per10MinuteEvent>(
                "周期清除过期的历史算力",
                LogEnum.Console,
                action: message => {
                    ClearOutOfDateHistory();
                });

            VirtualRoot.On<MineStopedEvent>(
                "停止挖矿后产生一次0算力",
                LogEnum.Console,
                action: message => {
                    now = DateTime.Now;
                    foreach (var gpu in _root.GpuSet) {
                        SetCurrentSpeed(gpuIndex: gpu.Index, speed: 0.0, isDual: false, now: now);
                        if (message.MineContext is IDualMineContext dualMineContext) {
                            SetCurrentSpeed(gpuIndex: gpu.Index, speed: 0.0, isDual: true, now: now);
                        }
                    }
                });

            VirtualRoot.On<MineStartedEvent>(
                "挖矿开始时产生一次0算力0份额",
                LogEnum.Console,
                action: message => {
                    now = DateTime.Now;
                    ICoinShare share = _root.CoinShareSet.GetOrCreate(message.MineContext.MainCoin.GetId());
                    share.AcceptShareCount = 0;
                    share.RejectShareCount = 0;
                    share.ShareOn = now;
                    VirtualRoot.Happened(new ShareChangedEvent(share));
                    foreach (var gpu in _root.GpuSet) {
                        SetCurrentSpeed(gpuIndex: gpu.Index, speed: 0.0, isDual: false, now: now);
                    }
                    if (message.MineContext is IDualMineContext dualMineContext) {
                        share = _root.CoinShareSet.GetOrCreate(dualMineContext.DualCoin.GetId());
                        share.AcceptShareCount = 0;
                        share.RejectShareCount = 0;
                        share.ShareOn = now;
                        VirtualRoot.Happened(new ShareChangedEvent(share));
                        foreach (var gpu in _root.GpuSet) {
                            SetCurrentSpeed(gpuIndex: gpu.Index, speed: 0.0, isDual: true, now: now);
                        }
                    }
                });
        }

        public void ClearOutOfDateHistory() {
            DateTime now = DateTime.Now;
            foreach (var historyList in _gpuSpeedHistory.Values) {
                var toRemoves = historyList.Where(a => a.MainCoinSpeed.SpeedOn.AddMinutes(_root.SpeedHistoryLengthByMinute) < now).ToArray();
                foreach (var item in toRemoves) {
                    historyList.Remove(item);
                }
            }
        }

        public IGpuSpeed CurrentSpeed(int gpuIndex) {
            if (!_currentGpuSpeed.ContainsKey(gpuIndex)) {
                return GpuSpeed.Empty;
            }
            return _currentGpuSpeed[gpuIndex];
        }

        private Guid _mainCoinId;
        public void SetCurrentSpeed(int gpuIndex, double speed, bool isDual, DateTime now) {
            GpuSpeed gpuSpeed = _currentGpuSpeed.Values.First(a => a.Gpu.Index == gpuIndex);
            if (gpuSpeed == null) {
                return;
            }
            Guid mainCoinId = _root.MinerProfile.CoinId;
            if (_mainCoinId != mainCoinId) {
                _mainCoinId = mainCoinId;
                foreach (var item in _gpuSpeedHistory) {
                    item.Value.Clear();
                }
                foreach (var item in _currentGpuSpeed.Values) {
                    item.UpdateMainCoinSpeed(0, now);
                    item.UpdateDualCoinSpeed(0, now);
                }
            }
            if (_gpuSpeedHistory.ContainsKey(gpuSpeed.Gpu.Index)) {
                _gpuSpeedHistory[gpuSpeed.Gpu.Index].Add(gpuSpeed.Clone());
            }
            bool isChanged = false;
            // 如果变化幅度大于等于百分之一
            if (isDual) {
                isChanged = gpuSpeed.DualCoinSpeed.SpeedOn.AddSeconds(10) < now || gpuSpeed.DualCoinSpeed.Value.IsChange(speed, 0.01);
                if (isChanged) {
                    gpuSpeed.UpdateDualCoinSpeed(speed, now);
                }
            }
            else {
                isChanged = gpuSpeed.MainCoinSpeed.SpeedOn.AddSeconds(10) < now || gpuSpeed.MainCoinSpeed.Value.IsChange(speed, 0.01);
                if (isChanged) {
                    gpuSpeed.UpdateMainCoinSpeed(speed, now);
                }
            }
            if (isChanged) {
                VirtualRoot.Happened(new GpuSpeedChangedEvent(isDualSpeed: isDual, gpuSpeed: gpuSpeed));
            }
        }

        public List<IGpuSpeed> GetGpuSpeedHistory(int index) {
            if (!_gpuSpeedHistory.ContainsKey(index)) {
                return new List<IGpuSpeed>();
            }
            return _gpuSpeedHistory[index];
        }

        public IEnumerator<IGpuSpeed> GetEnumerator() {
            return _currentGpuSpeed.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _currentGpuSpeed.Values.GetEnumerator();
        }
    }
}
