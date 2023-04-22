﻿using System.Collections.Generic;
using System.Linq;
using OxGFrame.AssetLoader;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OxGFrame.CoreFrame.GSFrame
{
    public class GSManager : FrameManager<GSBase>
    {
        private static readonly object _locker = new object();
        private static GSManager _instance = null;
        internal static GSManager GetInstance()
        {
            if (_instance == null)
            {
                lock (_locker)
                {
                    _instance = FindObjectOfType(typeof(GSManager)) as GSManager;
                    if (_instance == null) _instance = new GameObject(nameof(GSManager)).AddComponent<GSManager>();
                }
            }
            return _instance;
        }

        private void Awake()
        {
            this.gameObject.name = $"[{nameof(GSManager)}]";
            DontDestroyOnLoad(this);
        }

        #region 實作 Loading
        protected override GSBase Instantiate(GSBase gsBase, string assetName, AddIntoCache addIntoCache, Transform parent)
        {
            // instantiate 【GS Prefab】 (先指定 Instantiate Parent 為 GSRoot)
            GameObject instPref = Instantiate(gsBase.gameObject, (parent != null) ? parent : this.transform);

            // 激活檢查, 如果主體 Active 為 false 必須打開
            if (!instPref.activeSelf) instPref.SetActive(true);

            // Replace Name
            instPref.name = instPref.name.Replace("(Clone)", "");
            gsBase = instPref.GetComponent<GSBase>();
            if (gsBase == null) return null;

            addIntoCache?.Invoke(gsBase);

            gsBase.SetNames(assetName);
            // Clone 取得 GSBase 組件後, 也初始 GSBase 相關設定
            gsBase.BeginInit();
            // Clone 取得 GSBase 組件後, 也初始 GSBase 相關綁定組件設定
            gsBase.InitFirst();

            // >>> 需在 InitThis 之後, 以下設定開始生效 <<<

            // 透過 NodeName 設置 Parent
            this.SetParent(gsBase, parent);

            // 最後設置完畢後, 關閉 GameObject 的 Active 為 false
            gsBase.gameObject.SetActive(false);

            return gsBase;
        }
        #endregion

        #region 相關校正與設置
        /// <summary>
        /// 依照對應的 Node 類型設置母節點
        /// </summary>
        /// <param name="gsBase"></param>
        protected override bool SetParent(GSBase gsBase, Transform parent)
        {
            if (parent != null)
            {
                if (parent.gameObject.GetComponent<GSParent>() == null) parent.gameObject.AddComponent<GSParent>();
                gsBase.gameObject.transform.SetParent(parent);
                return true;
            }
            else
            {
                gsBase.gameObject.transform.SetParent(this.gameObject.transform);
                return true;
            }
        }
        #endregion

        #region Show
        public override async UniTask<GSBase> Show(int groupId, string assetName, object obj = null, string loadingUIAssetName = null, Progression progression = null, Transform parent = null)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            // 先取出 Stack 主體
            var stack = this.GetStackFromAllCache(assetName);

            // 判斷非多實例直接 return
            if (stack != null && !stack.allowInstantiate)
            {
                if (this.CheckIsShowing(assetName))
                {
                    Debug.LogWarning(string.Format("【GS】{0} already exists!!!", assetName));
                    return null;
                }
            }

            await this.ShowLoading(groupId, loadingUIAssetName); // 開啟預顯加載 UI

            var gsBase = await this.LoadIntoAllCache(assetName, progression, false, parent);
            if (gsBase == null)
            {
                Debug.LogWarning(string.Format("Asset not found at this path!!!【GS】: {0}", assetName));
                return null;
            }

            gsBase.SetGroupId(groupId);
            gsBase.SetHidden(false);
            await this.LoadAndDisplay(gsBase, obj);

            Debug.Log(string.Format("Show GS: 【{0}】", assetName));

            this.CloseLoading(loadingUIAssetName); // 執行完畢後, 關閉預顯加載 UI

            return gsBase;
        }

        //public override async UniTask<GSBase> Show(int groupId, string bundleName, string assetName, object obj = null, string loadingUIBundleName = null, string loadingUIAssetName = null, Progression progression = null)
        //{
        //    if (string.IsNullOrEmpty(bundleName) && string.IsNullOrEmpty(assetName)) return null;

        //    // 先取出 Stack 主體
        //    var stack = this.GetStackFromAllCache(assetName);

        //    // 判斷非多實例直接 return
        //    if (stack != null && !stack.allowInstantiate)
        //    {
        //        if (this.CheckIsShowing(assetName))
        //        {
        //            Debug.LogWarning(string.Format("【GS】{0} already exists!!!", assetName));
        //            return null;
        //        }
        //    }

        //    await this.ShowLoading(groupId, loadingUIBundleName, loadingUIAssetName); // 開啟預顯加載 UI

        //    var gsBase = await this.LoadIntoAllCache(bundleName, assetName, progression, false);
        //    if (gsBase == null)
        //    {
        //        Debug.LogWarning(string.Format("Asset not found at this path!!!【GS】: {0}", assetName));
        //        return null;
        //    }

        //    gsBase.SetGroupId(groupId);
        //    gsBase.SetHidden(false);
        //    await this.LoadAndDisplay(gsBase, obj);

        //    Debug.Log(string.Format("Show GS: 【{0}】", assetName));

        //    this.CloseLoading(loadingUIAssetName); // 執行完畢後, 關閉預顯加載 UI

        //    return gsBase;
        //}
        #endregion

        #region Close
        /// <summary>
        /// 將 Close 方法封裝 (由接口 Close 與 CloseAll 統一調用)
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="disableDoSub"></param>
        /// <param name="forceDestroy"></param>
        /// <param name="doAll"></param>
        private void _Close(string assetName, bool disableDoSub, bool forceDestroy, bool doAll)
        {
            if (string.IsNullOrEmpty(assetName) || !this.HasStackInAllCache(assetName)) return;

            if (doAll)
            {
                FrameStack<GSBase> stack = this.GetStackFromAllCache(assetName);
                foreach (var gsBase in stack.cache.ToArray())
                {
                    gsBase.SetHidden(false);
                    this.ExitAndHide(gsBase, disableDoSub);

                    if (forceDestroy) this.Destroy(gsBase, assetName);
                    else if (gsBase.allowInstantiate) this.Destroy(gsBase, assetName);
                    else if (gsBase.onCloseAndDestroy) this.Destroy(gsBase, assetName);
                }
            }
            else
            {
                GSBase gsBase = this.PeekStackFromAllCache(assetName);
                if (gsBase == null) return;

                gsBase.SetHidden(false);
                this.ExitAndHide(gsBase, disableDoSub);

                if (forceDestroy) this.Destroy(gsBase, assetName);
                else if (gsBase.allowInstantiate) this.Destroy(gsBase, assetName);
                else if (gsBase.onCloseAndDestroy) this.Destroy(gsBase, assetName);
            }

            Debug.Log(string.Format("Close GS: 【{0}】", assetName));
        }

        public override void Close(string assetName, bool disableDoSub = false, bool forceDestroy = false)
        {
            // 如果沒有強制 Destroy + 不是顯示狀態則直接 return
            if (!forceDestroy && !this.CheckIsShowing(assetName)) return;
            this._Close(assetName, disableDoSub, forceDestroy, false);
        }

        public override void CloseAll(bool disableDoSub = false, bool forceDestroy = false, params string[] withoutAssetNames)
        {
            if (this._dictAllCache.Count == 0) return;

            foreach (FrameStack<GSBase> stack in this._dictAllCache.Values.ToArray())
            {
                // prevent preload mode
                if (stack.Count() == 0) continue;

                string assetName = stack.assetName;

                var gsBase = stack.Peek();

                // 檢查排除執行的 GS
                bool checkWithout = false;
                if (withoutAssetNames.Length > 0)
                {
                    for (int i = 0; i < withoutAssetNames.Length; i++)
                    {
                        if (assetName == withoutAssetNames[i]) checkWithout = true;
                    }
                }

                // 排除在外的 GS 直接略過處理
                if (checkWithout) continue;

                // 如果沒有強制 Destroy + 不是顯示狀態則直接略過處理
                if (!forceDestroy && !this.CheckIsShowing(gsBase)) continue;

                // 如有啟用 CloseAll 需跳過開關, 則不列入關閉執行
                if (gsBase.gsSetting.whenCloseAllToSkip) continue;

                this._Close(assetName, disableDoSub, forceDestroy, true);
            }
        }

        public override void CloseAll(int groupId, bool disableDoSub = false, bool forceDestroy = false, params string[] withoutAssetNames)
        {
            if (this._dictAllCache.Count == 0) return;

            foreach (FrameStack<GSBase> stack in this._dictAllCache.Values.ToArray())
            {
                // prevent preload mode
                if (stack.Count() == 0) continue;

                string assetName = stack.assetName;

                var gsBase = stack.Peek();

                if (gsBase.groupId != groupId) continue;

                // 檢查排除執行的 GS
                bool checkWithout = false;
                if (withoutAssetNames.Length > 0)
                {
                    for (int i = 0; i < withoutAssetNames.Length; i++)
                    {
                        if (assetName == withoutAssetNames[i]) checkWithout = true;
                    }
                }

                // 排除在外的 GS 直接略過處理
                if (checkWithout) continue;

                // 如果沒有強制 Destroy + 不是顯示狀態則直接略過處理
                if (!forceDestroy && !this.CheckIsShowing(gsBase)) continue;

                // 如有啟用 CloseAll 需跳過開關, 則不列入關閉執行
                if (gsBase.gsSetting.whenCloseAllToSkip) continue;

                this._Close(assetName, disableDoSub, forceDestroy, true);
            }
        }
        #endregion

        #region Reveal
        /// <summary>
        /// 將 Reveal 方法封裝 (由接口 Reveal 與 RevealAll 統一調用)
        /// </summary>
        /// <param name="assetName"></param>
        private void _Reveal(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || !this.HasStackInAllCache(assetName)) return;

            if (this.CheckIsShowing(assetName))
            {
                Debug.LogWarning(string.Format("【GS】{0} Already Reveal!!!", assetName));
                return;
            }

            FrameStack<GSBase> stack = this.GetStackFromAllCache(assetName);
            foreach (var gsBase in stack.cache.ToArray())
            {
                if (!gsBase.isHidden) return;

                this.LoadAndDisplay(gsBase).Forget();

                Debug.Log(string.Format("Reveal GS: 【{0}】", assetName));
            }
        }

        public override void Reveal(string assetName)
        {
            this._Reveal(assetName);
        }

        public override void RevealAll()
        {
            if (this._dictAllCache.Count == 0) return;

            foreach (FrameStack<GSBase> stack in this._dictAllCache.Values.ToArray())
            {
                // prevent preload mode
                if (stack.Count() == 0) continue;

                string assetName = stack.assetName;

                var gsBase = stack.Peek();

                if (!gsBase.isHidden) continue;

                this._Reveal(assetName);
            }
        }

        public override void RevealAll(int groupId)
        {
            if (this._dictAllCache.Count == 0) return;

            foreach (FrameStack<GSBase> stack in this._dictAllCache.Values.ToArray())
            {
                // prevent preload mode
                if (stack.Count() == 0) continue;

                string assetName = stack.assetName;

                var gsBase = stack.Peek();

                if (gsBase.groupId != groupId) continue;

                if (!gsBase.isHidden) continue;

                this._Reveal(assetName);
            }
        }
        #endregion

        #region Hide
        /// <summary>
        /// 將 Hide 方法封裝 (由接口 Hide 與 HideAll 統一調用)
        /// </summary>
        /// <param name="assetName"></param>
        private void _Hide(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || !this.HasStackInAllCache(assetName)) return;

            FrameStack<GSBase> stack = this.GetStackFromAllCache(assetName);

            if (!this.CheckIsShowing(stack.Peek())) return;

            foreach (var gsBase in stack.cache.ToArray())
            {
                gsBase.SetHidden(true);
                this.ExitAndHide(gsBase);
            }

            Debug.Log(string.Format("Hide GS: 【{0}】", assetName));
        }

        public override void Hide(string assetName)
        {
            this._Hide(assetName);
        }

        public override void HideAll(params string[] withoutAssetNames)
        {
            if (this._dictAllCache.Count == 0) return;

            foreach (FrameStack<GSBase> stack in this._dictAllCache.Values.ToArray())
            {
                // prevent preload mode
                if (stack.Count() == 0) continue;

                string assetName = stack.assetName;

                var gsBase = stack.Peek();

                // 檢查排除執行的 GS
                bool checkWithout = false;
                if (withoutAssetNames.Length > 0)
                {
                    for (int i = 0; i < withoutAssetNames.Length; i++)
                    {
                        if (assetName == withoutAssetNames[i]) checkWithout = true;
                    }
                }

                // 排除在外的 GS 直接略過處理
                if (checkWithout) continue;

                // 如有啟用 HideAll 需跳過開關, 則不列入關閉執行
                if (gsBase.gsSetting.whenHideAllToSkip) continue;

                this._Hide(assetName);
            }
        }

        public override void HideAll(int groupId, params string[] withoutAssetNames)
        {
            if (this._dictAllCache.Count == 0) return;

            foreach (FrameStack<GSBase> stack in this._dictAllCache.Values.ToArray())
            {
                // prevent preload mode
                if (stack.Count() == 0) continue;

                string assetName = stack.assetName;

                var gsBase = stack.Peek();

                if (gsBase.groupId != groupId) continue;

                // 檢查排除執行的 GS
                bool checkWithout = false;
                if (withoutAssetNames.Length > 0)
                {
                    for (int i = 0; i < withoutAssetNames.Length; i++)
                    {
                        if (assetName == withoutAssetNames[i]) checkWithout = true;
                    }
                }

                // 排除在外的 GS 直接略過處理
                if (checkWithout) continue;

                // 如有啟用 HideAll 需跳過開關, 則不列入關閉執行
                if (gsBase.gsSetting.whenHideAllToSkip) continue;

                this._Hide(assetName);
            }
        }
        #endregion

        #region 顯示場景 & 關閉場景
        protected override async UniTask LoadAndDisplay(GSBase gsBase, object obj = null)
        {
            if (!gsBase.isHidden) await gsBase.PreInit();
            gsBase.Display(obj);
        }

        protected override void ExitAndHide(GSBase gsBase, bool disableDoSub = false)
        {
            gsBase.Hide(disableDoSub);
        }
        #endregion
    }
}