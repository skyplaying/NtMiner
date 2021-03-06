﻿using LiteDB;
using NTMiner.Core.MinerServer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTMiner.Core.Impl {
    public class CalcConfigSet : SetBase, ICalcConfigSet {
        private Dictionary<string, CalcConfigData> _dicByCode = new Dictionary<string, CalcConfigData>(StringComparer.OrdinalIgnoreCase);

        public CalcConfigSet() {
        }

        protected override void Init() {
            using (LiteDatabase db = AppRoot.CreateLocalDb()) {
                var col = db.GetCollection<CalcConfigData>();
                foreach (var item in col.FindAll()) {
                    _dicByCode.Add(item.CoinCode, item);
                }
            }
        }

        /// <summary>
        /// <see cref="ICalcConfigSet.Gets(IEnumerable{string})"/>
        /// </summary>
        /// <param name="coinCodes">null或长度0表示读取全部</param>
        /// <returns></returns>
        public List<CalcConfigData> Gets(string[] coinCodes) {
            InitOnece();
            if (coinCodes == null || coinCodes.Length == 0) {
                return _dicByCode.Values.ToList();
            }
            else {
                List<CalcConfigData> list = new List<CalcConfigData>();
                foreach (var coinCode in coinCodes) {
                    if (_dicByCode.TryGetValue(coinCode, out CalcConfigData value)) {
                        list.Add(value);
                    }
                }
                return list;
            }
        }

        public void SaveCalcConfigs(List<CalcConfigData> data) {
            InitOnece();
            if (data == null || data.Count == 0) {
                return;
            }
            lock (_dicByCode) {
                var dic = new Dictionary<string, CalcConfigData>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in data) {
                    if (dic.TryGetValue(item.CoinCode, out CalcConfigData entity)) {
                        entity.Update(item);
                    }
                    else {
                        dic.Add(item.CoinCode, item);
                    }
                }
                using (LiteDatabase db = AppRoot.CreateLocalDb()) {
                    var col = db.GetCollection<CalcConfigData>();
                    col.Delete(Query.All());
                    col.Insert(dic.Values);
                }
                _dicByCode = dic;
            }
        }
    }
}
