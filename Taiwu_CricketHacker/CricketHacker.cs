using FrameWork;
using FrameWork.ModSystem;
using Game.Components.ListStyleGeneralScroll.Item;
using Game.Views.Cricket;
using Game.Views.Cricket.Combat;
using GameData.Domains.Item;
using GameData.Domains.TaiwuEvent.DisplayEvent;
using GameDataExtensions;
using HarmonyLib;
using System;
using System.Reflection;
using TaiwuModdingLib.Core.Plugin;
using TMPro;
using UnityEngine;

namespace Taiwu_CricketHacker
{
    [PluginConfig("CricketHacker", "XeonSatan", "1")]
    public class CricketHacker : TaiwuRemakePlugin
    {
        // 游戏类影响清单：这里的“修改”指运行时 Harmony 补丁影响，不会改写游戏 DLL 文件。
        // 直接挂补丁的游戏类/方法：
        // - Game.Views.Cricket.ViewCatchCricket.InitCatchPlace：抓蛐蛐地点初始化后显示蛐蛐名称。
        // - Game.Views.Cricket.ViewCatchCricket.OnClickCatchPlace(int)：点击捕捉点前执行强制捕捉逻辑。
        // - Game.Views.Cricket.ViewCatchCricket.FinishCatch、OnDisable：捕捉结束或界面关闭时立即清理名称。
        // - Game.Views.Cricket.Combat.CricketJar.SetVisible(bool)：蛐蛐对战时强制显示对手罐中蛐蛐。
        // - Game.Views.Cricket.CricketBettingRewardItemView.SetData(int, CricketWagerData, Action<int>)：赌约奖励三选一刷新后显示隐藏蛐蛐。
        // 只读写但不单独挂补丁的游戏界面类/组件：CardItem、CricketViewNew、TooltipInvoker、CImage、TextMeshProUGUI。
        // 最早代码版本挂的是旧类 UI_CatchCricket；正式版已改为 ViewCatchCricket，蛐蛐名称/品级也改用 GameDataExtensions 的正式版扩展方法。
        private const string CatchPlaceListFieldName = "_catchPlaceList";
        private const string CatchPlaceRootFieldName = "catchPlaceRoot";
        private const string CricketLabelOverlayName = "CricketHackerLabelOverlay";
        private const string CricketLabelName = "cricketShowName";
        private const int CricketLabelFontSize = 28;
        private const short ForceCatchSingLevel = 1000;
        private static readonly Vector3 CricketLabelLocalPosition = new Vector3(100f, -100f, 0f);
        private static readonly Vector2 CricketLabelSize = new Vector2(260f, 80f);
        private const bool DebugLogging = false;
        private const int MaxVerboseLogCount = 20;

        public static bool EnableFlag = true;
        public static bool EnableCatchCricketFlag = true;
        public static bool SetSingFlag;
        public static bool EnableCombatFlag = true;

        private static ViewCatchCricket _lastCatchCricketView;
        private static int _catchPatchHitCount;
        private static int _catchClickPatchHitCount;
        private static int _combatPatchHitCount;
        private static int _bettingPatchHitCount;
        private static TMP_FontAsset _labelFont;
        private static Material _labelFontMaterial;
        private static bool _labelFontLogPrinted;

        private Harmony harmony;

        // MOD 卸载时清理已创建的透视标签，并撤销本 MOD 注册的 Harmony 补丁。
        public override void Dispose()
        {
            Log("Dispose. Cleaning labels and unpatching.");
            Clean();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }

        // MOD 加载时初始化 Harmony，并挂载抓蛐蛐、对战、赌约奖励三个透视补丁。
        public override void Initialize()
        {
            Log("Initialize begin.");
            Log("Assembly-CSharp=" + typeof(ViewCatchCricket).Assembly.Location);
            Log("ViewCatchCricket=" + typeof(ViewCatchCricket).FullName);
            Log("CricketJar=" + typeof(CricketJar).FullName);
            Log("CricketBettingRewardItemView=" + typeof(CricketBettingRewardItemView).FullName);
            harmony = new Harmony("Taiwu_CricketHacker.CricketHacker");
            PatchCatchCricket();
            PatchCombatVisibility();
            PatchBettingRewards();
            Log("Initialize end. Defaults: EnableFlag=" + EnableFlag + ", EnableCatchCricketFlag=" + EnableCatchCricketFlag + ", SetSingFlag=" + SetSingFlag + ", EnableCombatFlag=" + EnableCombatFlag);
        }

        // 注册抓蛐蛐界面的补丁，包括生成透视标签和可选的强制捕捉。
        // 目标游戏类：ViewCatchCricket；目标方法：InitCatchPlace、OnClickCatchPlace(int)。
        private void PatchCatchCricket()
        {
            PatchPostfix(
                AccessTools.Method(typeof(ViewCatchCricket), "InitCatchPlace"),
                nameof(InitCatchPlace_PostPatch));

            PatchPrefix(
                AccessTools.Method(typeof(ViewCatchCricket), "OnClickCatchPlace", new Type[] { typeof(int) }),
                nameof(OnClickCatchPlace_PrePatch));

            PatchPrefix(
                AccessTools.Method(typeof(ViewCatchCricket), "FinishCatch"),
                nameof(FinishCatch_PrePatch));

            PatchPrefix(
                AccessTools.Method(typeof(ViewCatchCricket), "OnDisable"),
                nameof(CatchCricketOnDisable_PrePatch));
        }

        // 注册蛐蛐对战罐子显示状态的补丁，用于强制显示对手蛐蛐。
        // 目标游戏类：CricketJar；目标方法：SetVisible(bool)。
        private void PatchCombatVisibility()
        {
            PatchPrefix(
                AccessTools.Method(typeof(CricketJar), "SetVisible", new Type[] { typeof(bool) }),
                nameof(RevealCombatCricket_PrePatch));
        }

        // 注册赌约奖励项刷新的补丁，用于显示奖励候选中的隐藏蛐蛐。
        // 目标游戏类：CricketBettingRewardItemView；目标方法：SetData(int, CricketWagerData, Action<int>)。
        private void PatchBettingRewards()
        {
            PatchPostfix(
                AccessTools.Method(typeof(CricketBettingRewardItemView), "SetData",
                    new Type[] { typeof(int), typeof(CricketWagerData), typeof(Action<int>) }),
                nameof(RevealBettingReward_PostPatch));
        }

        // 按方法名查找本类前置补丁，并把它挂到目标游戏方法上。
        private void PatchPrefix(MethodBase original, string prefixName)
        {
            MethodInfo prefix = AccessTools.Method(typeof(CricketHacker), prefixName);
            if (original != null && prefix != null)
            {
                harmony.Patch(original, prefix: new HarmonyMethod(prefix));
                Log("Patch prefix success: " + DescribeMethod(original) + " -> " + prefixName);
            }
            else
            {
                LogWarning("Patch prefix failed: " + prefixName + ", original=" + DescribeMethod(original) + ", prefix=" + DescribeMethod(prefix));
            }
        }

        // 按方法名查找本类后置补丁，并把它挂到目标游戏方法上。
        private void PatchPostfix(MethodBase original, string postfixName)
        {
            MethodInfo postfix = AccessTools.Method(typeof(CricketHacker), postfixName);
            if (original != null && postfix != null)
            {
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                Log("Patch postfix success: " + DescribeMethod(original) + " -> " + postfixName);
            }
            else
            {
                LogWarning("Patch postfix failed: " + postfixName + ", original=" + DescribeMethod(original) + ", postfix=" + DescribeMethod(postfix));
            }
        }

        // 读取 MOD 设置界面的开关状态，同步到运行时静态开关。
        public override void OnModSettingUpdate()
        {
            Log("OnModSettingUpdate before: EnableFlag=" + EnableFlag + ", EnableCatchCricketFlag=" + EnableCatchCricketFlag + ", SetSingFlag=" + SetSingFlag + ", EnableCombatFlag=" + EnableCombatFlag);
            ModManager.GetSetting(ModIdStr, "EnableFlag", val: ref EnableFlag);
            ModManager.GetSetting(ModIdStr, "EnableCatchCricketFlag", val: ref EnableCatchCricketFlag);
            ModManager.GetSetting(ModIdStr, "SetSingFlag", val: ref SetSingFlag);
            ModManager.GetSetting(ModIdStr, "EnableCombatFlag", val: ref EnableCombatFlag);
            Log("OnModSettingUpdate after: ModId=" + ModIdStr + ", EnableFlag=" + EnableFlag + ", EnableCatchCricketFlag=" + EnableCatchCricketFlag + ", SetSingFlag=" + SetSingFlag + ", EnableCombatFlag=" + EnableCombatFlag);

            if (!IsCatchCricketRevealEnabled())
            {
                Clean();
            }
            else if (_lastCatchCricketView != null)
            {
                RefreshCatchCricketLabels(_lastCatchCricketView);
            }
        }

        // 抓蛐蛐地点初始化后，为每个捕捉点创建或刷新显示蛐蛐名称的透视标签。
        public static void InitCatchPlace_PostPatch(ViewCatchCricket __instance)
        {
            try
            {
                _catchPatchHitCount++;
                LogVerbose(ref _catchPatchHitCount, "InitCatchPlace_PostPatch hit. instanceNull=" + (__instance == null) + ", EnableFlag=" + EnableFlag + ", EnableCatchCricketFlag=" + EnableCatchCricketFlag);
                if (__instance == null)
                {
                    return;
                }

                _lastCatchCricketView = __instance;

                if (!IsCatchCricketRevealEnabled())
                {
                    LogVerbose(ref _catchPatchHitCount, "InitCatchPlace_PostPatch skipped because catch cricket reveal is disabled.");
                    Clean(__instance);
                    return;
                }

                RefreshCatchCricketLabels(__instance);
            }
            catch (Exception ex)
            {
                LogException("InitCatchPlace_PostPatch exception", ex);
            }
        }

        private static bool IsCatchCricketRevealEnabled()
        {
            return EnableFlag && EnableCatchCricketFlag;
        }

        private static void RefreshCatchCricketLabels(ViewCatchCricket view)
        {
            if (view == null)
            {
                return;
            }

            ViewCatchCricket.CricketPlaceInfo[] placeList = GetCatchPlaceList(view);
            RectTransform rootRT = GetCatchPlaceRoot(view);
            if (placeList == null)
            {
                LogWarning("InitCatchPlace_PostPatch: _catchPlaceList is null.");
                return;
            }

            CacheLabelFont(view);
            RectTransform labelOverlay = GetOrCreateLabelOverlay(view);
            if (labelOverlay == null)
            {
                LogWarning("InitCatchPlace_PostPatch: label overlay creation failed.");
                return;
            }

            if (rootRT == null)
            {
                LogWarning("InitCatchPlace_PostPatch: catchPlaceRoot is null. Will try PlaceView fallback.");
            }

            int placeCount = placeList.Length;
            int labelCount = 0;
            int nullInfoCount = 0;
            int nullParentCount = 0;
            LogVerbose(ref _catchPatchHitCount, "InitCatchPlace_PostPatch: placeList=" + placeList.Length + ", rootChildCount=" + (rootRT == null ? -1 : rootRT.childCount) + ", placeCount=" + placeCount);
            for (int i = 0; i < placeCount; i++)
            {
                ViewCatchCricket.CricketPlaceInfo info = placeList[i];
                if (info == null)
                {
                    nullInfoCount++;
                    continue;
                }

                ValueTuple<short, short> cricketId = new ValueTuple<short, short>(info.CricketColorId, info.CricketPartsId);
                string cricketName = cricketId.CalcCricketName();
                int cricketLevel = cricketId.CalcCricketGrade();
                string nameWithColor = Extentions.SetGradeColor(cricketName, cricketLevel);

                Transform parent = GetCatchPlaceTransform(info, rootRT, i);
                if (parent != null)
                {
                    // 清理上一版直接挂在捕捉点下的独立 Canvas 标签。
                    RemoveCricketLabel(parent);
                    SetCricketLabel(labelOverlay, parent, i, nameWithColor);
                    labelCount++;
                    if (i < 3)
                    {
                        LogVerbose(ref _catchPatchHitCount, "InitCatchPlace_PostPatch sample[" + i + "]: color=" + info.CricketColorId + ", part=" + info.CricketPartsId + ", level=" + cricketLevel + ", name=" + cricketName + ", parent=" + GetTransformPath(parent));
                    }
                }
                else
                {
                    nullParentCount++;
                }
            }

            labelOverlay.SetAsLastSibling();
            Log("InitCatchPlace_PostPatch done. labels=" + labelCount + ", nullInfo=" + nullInfoCount + ", nullParent=" + nullParentCount);
        }

        // 清理最近一次抓蛐蛐界面里由本 MOD 创建的透视标签。
        public static void Clean()
        {
            Clean(_lastCatchCricketView);
        }

        // 清理指定抓蛐蛐界面里由本 MOD 创建的透视标签。
        private static void Clean(ViewCatchCricket view)
        {
            if (view == null)
            {
                return;
            }

            RemoveCricketLabelOverlay(view);

            // 兼容清理旧版直接挂在捕捉点下的标签。
            ViewCatchCricket.CricketPlaceInfo[] placeList = GetCatchPlaceList(view);
            RectTransform rootRT = GetCatchPlaceRoot(view);
            if (placeList != null)
            {
                int placeCount = placeList.Length;
                for (int i = 0; i < placeCount; i++)
                {
                    Transform parent = GetCatchPlaceTransform(placeList[i], rootRT, i);
                    RemoveCricketLabel(parent);
                }
            }
            else if (rootRT != null)
            {
                for (int i = 0; i < rootRT.childCount; i++)
                {
                    RemoveCricketLabel(rootRT.GetChild(i));
                }
            }
        }

        // 点击捕捉点前，在强制捕捉开关开启时临时提高该位置的叫声等级。
        public static void OnClickCatchPlace_PrePatch(ViewCatchCricket __instance, int index)
        {
            try
            {
                _catchClickPatchHitCount++;
                LogVerbose(ref _catchClickPatchHitCount, "OnClickCatchPlace_PrePatch hit. index=" + index + ", instanceNull=" + (__instance == null) + ", EnableFlag=" + EnableFlag + ", SetSingFlag=" + SetSingFlag);
                if (!EnableFlag || !SetSingFlag || __instance == null)
                {
                    return;
                }

                ViewCatchCricket.CricketPlaceInfo[] placeList = GetCatchPlaceList(__instance);
                if (placeList == null || index < 0 || index >= placeList.Length || placeList[index] == null)
                {
                    LogWarning("OnClickCatchPlace_PrePatch skipped. placeListNull=" + (placeList == null) + ", index=" + index + ", length=" + (placeList == null ? -1 : placeList.Length));
                    return;
                }

                short oldSingLevel = placeList[index].SingLevel;
                placeList[index].SingLevel = ForceCatchSingLevel;
                Log("OnClickCatchPlace_PrePatch set SingLevel. index=" + index + ", old=" + oldSingLevel + ", new=" + ForceCatchSingLevel);
            }
            catch (Exception ex)
            {
                LogException("OnClickCatchPlace_PrePatch exception", ex);
            }
        }

        // 蛐蛐对战刷新罐子可见性前，将隐藏状态改为显示。
        public static void RevealCombatCricket_PrePatch(ref bool visible)
        {
            try
            {
                _combatPatchHitCount++;
                bool oldVisible = visible;
                if (EnableFlag && EnableCombatFlag)
                {
                    visible = true;
                }

                if (_combatPatchHitCount <= MaxVerboseLogCount || (!oldVisible && visible))
                {
                    Log("RevealCombatCricket_PrePatch hit. oldVisible=" + oldVisible + ", newVisible=" + visible + ", EnableFlag=" + EnableFlag + ", EnableCombatFlag=" + EnableCombatFlag + ", hit=" + _combatPatchHitCount);
                }
            }
            catch (Exception ex)
            {
                LogException("RevealCombatCricket_PrePatch exception", ex);
            }
        }

        // 赌约奖励项设置数据后，强制显示奖励项中所有候选蛐蛐的名称、图像和提示。
        public static void RevealBettingReward_PostPatch(CricketBettingRewardItemView __instance, CricketWagerData reward)
        {
            try
            {
                _bettingPatchHitCount++;
                LogVerbose(ref _bettingPatchHitCount, "RevealBettingReward_PostPatch hit. instanceNull=" + (__instance == null) + ", rewardNull=" + (reward == null) + ", cricketsNull=" + (reward == null || reward.Crickets == null) + ", EnableFlag=" + EnableFlag + ", EnableCombatFlag=" + EnableCombatFlag);
                if (!EnableFlag || !EnableCombatFlag || __instance == null || reward == null || reward.Crickets == null)
                {
                    return;
                }

                CardItem[] cricketList = Traverse.Create(__instance).Field("cricketList").GetValue<CardItem[]>();
                TextMeshProUGUI[] cricketNames = Traverse.Create(__instance).Field("cricketNames").GetValue<TextMeshProUGUI[]>();
                CImage[] cricketBoxImages = Traverse.Create(__instance).Field("cricketBoxImages").GetValue<CImage[]>();
                Sprite visibleBoxSprite = Traverse.Create(__instance).Field("cricketBoxVisibleSprite").GetValue<Sprite>();

                if (cricketList == null)
                {
                    LogWarning("RevealBettingReward_PostPatch: cricketList is null.");
                    return;
                }

                int count = Math.Min(cricketList.Length, reward.Crickets.Count);
                int revealedCount = 0;
                Log("RevealBettingReward_PostPatch data. rewardCrickets=" + reward.Crickets.Count + ", cricketList=" + cricketList.Length + ", names=" + (cricketNames == null ? -1 : cricketNames.Length) + ", boxes=" + (cricketBoxImages == null ? -1 : cricketBoxImages.Length) + ", visibleBoxSpriteNull=" + (visibleBoxSprite == null));
                for (int i = 0; i < count; i++)
                {
                    CardItem cricketItem = cricketList[i];
                    if (cricketItem == null)
                    {
                        LogWarning("RevealBettingReward_PostPatch: cricketItem[" + i + "] is null.");
                        continue;
                    }

                    cricketItem.gameObject.SetActive(true);

                    if (cricketNames != null && i < cricketNames.Length && cricketNames[i] != null)
                    {
                        cricketNames[i].text = reward.Crickets[i].GetName(true);
                    }

                    RevealBettingCricketItem(cricketItem, reward.Crickets[i], i);

                    if (cricketBoxImages != null && i < cricketBoxImages.Length && cricketBoxImages[i] != null)
                    {
                        if (visibleBoxSprite != null)
                        {
                            cricketBoxImages[i].sprite = visibleBoxSprite;
                        }

                        cricketBoxImages[i].SetEnabled(cricketBoxImages[i].sprite != null);
                    }

                    revealedCount++;
                }

                Log("RevealBettingReward_PostPatch done. revealed=" + revealedCount + ", count=" + count);
            }
            catch (Exception ex)
            {
                LogException("RevealBettingReward_PostPatch exception", ex);
            }
        }

        // 从抓蛐蛐界面实例中读取私有的捕捉点数据数组。
        private static ViewCatchCricket.CricketPlaceInfo[] GetCatchPlaceList(ViewCatchCricket view)
        {
            return Traverse.Create(view).Field(CatchPlaceListFieldName).GetValue<ViewCatchCricket.CricketPlaceInfo[]>();
        }

        // 从抓蛐蛐界面实例中读取捕捉点根节点。
        private static RectTransform GetCatchPlaceRoot(ViewCatchCricket view)
        {
            return Traverse.Create(view).Field(CatchPlaceRootFieldName).GetValue<RectTransform>();
        }

        // 根据捕捉点数据优先取得实际位置节点，必要时回退到根节点子对象。
        private static Transform GetCatchPlaceTransform(ViewCatchCricket.CricketPlaceInfo info, RectTransform rootRT, int index)
        {
            if (info != null && info.PlaceView != null)
            {
                return info.PlaceView.transform;
            }

            if (rootRT != null && index >= 0 && index < rootRT.childCount)
            {
                return rootRT.GetChild(index);
            }

            return null;
        }

        // 在游戏原生 Canvas 内创建最后绘制的普通文字层，避免新增 Canvas 不参与该界面渲染。
        private static RectTransform GetOrCreateLabelOverlay(ViewCatchCricket view)
        {
            if (view == null)
            {
                return null;
            }

            Transform overlayTrans = view.transform.Find(CricketLabelOverlayName);
            RectTransform overlay = overlayTrans as RectTransform;
            if (overlay == null)
            {
                GameObject overlayObj = new GameObject(CricketLabelOverlayName, typeof(RectTransform));
                overlayObj.transform.SetParent(view.transform, false);
                overlay = overlayObj.GetComponent<RectTransform>();
            }

            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.localPosition = Vector3.zero;
            overlay.localScale = Vector3.one;
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            return overlay;
        }

        // 在普通覆盖层中创建文字，使所有名称在雾气之后由游戏原生 Canvas 统一绘制。
        private static void SetCricketLabel(RectTransform overlay, Transform placeTransform, int index, string textValue)
        {
            string labelName = CricketLabelName + "_" + index;
            Transform textTrans = overlay.Find(labelName);
            TextMeshProUGUI text;
            if (textTrans == null)
            {
                GameObject textObj = new GameObject(labelName, typeof(RectTransform));
                textObj.transform.SetParent(overlay, false);
                textObj.transform.localScale = Vector3.one;

                RectTransform rect = textObj.GetComponent<RectTransform>();
                rect.sizeDelta = CricketLabelSize;

                text = textObj.AddComponent<TextMeshProUGUI>();
                text.fontSize = CricketLabelFontSize;
                text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
            }
            else
            {
                text = textTrans.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    text = textTrans.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }

            Vector3 worldPosition = placeTransform.TransformPoint(CricketLabelLocalPosition);
            Vector3 overlayPosition = overlay.InverseTransformPoint(worldPosition);
            RectTransform textRect = text.rectTransform;
            textRect.localPosition = new Vector3(overlayPosition.x, overlayPosition.y, 0f);
            textRect.localScale = Vector3.one;
            textRect.sizeDelta = CricketLabelSize;

            ApplyLabelFont(text);
            text.fontSize = CricketLabelFontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = Color.white;
            text.alpha = 1f;
            text.text = textValue;
            text.transform.SetAsLastSibling();

            if (index < 3)
            {
                Canvas renderedCanvas = text.canvas;
                Log("Catch label[" + index + "] render state: active=" + text.gameObject.activeInHierarchy
                    + ", textEnabled=" + text.enabled
                    + ", renderedCanvas=" + (renderedCanvas == null ? "<null>" : GetTransformPath(renderedCanvas.transform))
                    + ", canvasOrder=" + (renderedCanvas == null ? -1 : renderedCanvas.sortingOrder)
                    + ", canvasLayer=" + (renderedCanvas == null ? "<null>" : renderedCanvas.sortingLayerName)
                    + ", font=" + (text.font == null ? "<null>" : text.font.name)
                    + ", overlaySize=" + overlay.rect.size
                    + ", localPos=" + textRect.localPosition
                    + ", worldPos=" + textRect.position
                    + ", size=" + textRect.rect.size
                    + ", text=" + textValue);
            }
        }

        // 捕捉流程结束时立即隐藏并清理透视文字，避免其残留在抓取动画上。
        public static void FinishCatch_PrePatch(ViewCatchCricket __instance)
        {
            Log("FinishCatch_PrePatch: removing catch cricket labels.");
            RemoveCricketLabelOverlay(__instance);
        }

        // 抓蛐蛐界面被直接关闭时兜底清理透视文字。
        public static void CatchCricketOnDisable_PrePatch(ViewCatchCricket __instance)
        {
            Log("CatchCricketOnDisable_PrePatch: removing catch cricket labels.");
            RemoveCricketLabelOverlay(__instance);
        }

        // 清理上一版或当前版本创建的全局文字覆盖层。
        private static void RemoveCricketLabelOverlay(ViewCatchCricket view)
        {
            if (view == null)
            {
                return;
            }

            Transform overlayTrans = view.transform.Find(CricketLabelOverlayName);
            if (overlayTrans != null)
            {
                // Destroy 会延迟到帧末，先禁用可保证文字在当前帧立即消失。
                overlayTrans.gameObject.SetActive(false);
                GameObject.Destroy(overlayTrans.gameObject);
            }
        }

        // 从游戏现有文本控件中缓存 TMP 字体，供新创建的透视标签复用。
        private static void CacheLabelFont(ViewCatchCricket view)
        {
            if (_labelFont != null || view == null)
            {
                return;
            }

            TextMeshProUGUI source = Traverse.Create(view).Field("cricketNameText").GetValue<TextMeshProUGUI>();
            if (source == null || source.font == null)
            {
                source = Traverse.Create(view).Field("cricketPoemEnText").GetValue<TextMeshProUGUI>();
            }

            if (source == null || source.font == null)
            {
                source = Traverse.Create(view).Field("catchPoemEnText").GetValue<TextMeshProUGUI>();
            }

            if (source == null || source.font == null)
            {
                TextMeshProUGUI[] texts = view.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].font != null)
                    {
                        source = texts[i];
                        break;
                    }
                }
            }

            if (source != null && source.font != null)
            {
                _labelFont = source.font;
                _labelFontMaterial = source.fontSharedMaterial;
                if (!_labelFontLogPrinted)
                {
                    Log("Cached label font from " + GetTransformPath(source.transform) + ": " + _labelFont.name);
                    _labelFontLogPrinted = true;
                }
            }
            else
            {
                LogWarning("CacheLabelFont failed: no TextMeshProUGUI font found under ViewCatchCricket.");
            }
        }

        // 把已缓存的 TMP 字体和材质应用到透视标签上。
        private static void ApplyLabelFont(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            if (_labelFont != null)
            {
                text.font = _labelFont;
            }

            if (_labelFontMaterial != null)
            {
                text.fontSharedMaterial = _labelFontMaterial;
            }
        }

        // 删除指定捕捉点节点下由本 MOD 创建的蛐蛐名称标签。
        private static void RemoveCricketLabel(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            Transform textTrans = parent.Find(CricketLabelName);
            if (textTrans != null)
            {
                GameObject.Destroy(textTrans.gameObject);
            }
        }

        // 显示赌约奖励中的单个蛐蛐卡片，并恢复图像、空白图层和 Tooltip 数据。
        private static void RevealBettingCricketItem(CardItem cricketItem, GameData.Domains.Item.Display.ItemDisplayData cricketData, int index)
        {
            CricketViewNew cricketView = cricketItem.CricketView;
            if (cricketView != null)
            {
                if (cricketView.skeletonGraphic != null)
                {
                    cricketView.skeletonGraphic.enabled = true;
                }

                CEmptyGraphic emptyGraphic = cricketView.GetComponent<CEmptyGraphic>();
                if (emptyGraphic != null)
                {
                    emptyGraphic.enabled = true;
                }

                TooltipInvoker cricketTip = cricketView.GetComponent<TooltipInvoker>();
                if (cricketTip != null)
                {
                    cricketTip.enabled = true;
                    if (cricketTip.RuntimeParam == null)
                    {
                        cricketTip.RuntimeParam = new ArgumentBox();
                    }

                    cricketTip.RuntimeParam.SetObject("ItemData", cricketData);
                    cricketTip.Refresh(false, -1);
                }

                if (_bettingPatchHitCount <= MaxVerboseLogCount)
                {
                    Log("RevealBettingCricketItem[" + index + "] view ok. color=" + cricketData.CricketColorId + ", part=" + cricketData.CricketPartId + ", skeletonNull=" + (cricketView.skeletonGraphic == null) + ", cricketTipNull=" + (cricketTip == null));
                }
            }
            else
            {
                LogWarning("RevealBettingCricketItem[" + index + "]: CricketView is null.");
            }

            TooltipInvoker itemTip = cricketItem.GetComponent<TooltipInvoker>();
            if (itemTip != null)
            {
                itemTip.enabled = true;
            }
            else
            {
                LogWarning("RevealBettingCricketItem[" + index + "]: CardItem TooltipInvoker is null.");
            }
        }

        // 把反射取得的方法转换成便于日志阅读的完整名称。
        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
            {
                return "<null>";
            }

            Type declaringType = method.DeclaringType;
            return (declaringType == null ? "<no type>" : declaringType.FullName) + "." + method.Name;
        }

        // 生成 Transform 在场景层级中的完整路径，便于日志定位界面节点。
        private static string GetTransformPath(Transform trans)
        {
            if (trans == null)
            {
                return "<null>";
            }

            string path = trans.name;
            while (trans.parent != null)
            {
                trans = trans.parent;
                path = trans.name + "/" + path;
            }

            return path;
        }

        // 限制重复调试日志的输出次数，避免日志文件被高频刷新刷屏。
        private static void LogVerbose(ref int counter, string message)
        {
            if (counter <= MaxVerboseLogCount)
            {
                Log(message);
            }
        }

        // 输出普通调试日志。
        private static void Log(string message)
        {
            if (DebugLogging)
            {
                Debug.Log("[CricketHacker] " + message);
            }
        }

        // 输出警告级调试日志。
        private static void LogWarning(string message)
        {
            if (DebugLogging)
            {
                Debug.LogWarning("[CricketHacker] " + message);
            }
        }

        // 输出异常日志，包含异常消息和堆栈。
        private static void LogException(string message, Exception ex)
        {
            Debug.LogError("[CricketHacker] " + message + ": " + ex.Message + "\n" + ex.StackTrace);
        }
    }
}
