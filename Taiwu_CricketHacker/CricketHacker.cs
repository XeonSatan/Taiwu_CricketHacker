using Config;
using FrameWork.ModSystem;
using HarmonyLib;
using System;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace Taiwu_CricketHacker
{
    [PluginConfig("CricketHacker", "XeonSatan", "1")]
    public class CricketHacker : TaiwuRemakePlugin
    {
        //启用开关
        public static bool EnableFlag;
        //必捉开关
        public static bool SetSingFlag;
        //Harmony
        Harmony harmony;

        public override void Dispose()
        {
            Clean();
            if (harmony != null)
                harmony.UnpatchSelf();
        }
        public override void Initialize()
        {
            harmony = Harmony.CreateAndPatchAll(typeof(CricketHacker));
        }

        public override void OnModSettingUpdate()
        {
            ModManager.GetSetting(ModIdStr, "EnableFlag", val: ref EnableFlag);
            ModManager.GetSetting(ModIdStr, "SetSingFlag", val: ref SetSingFlag);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UI_CatchCricket), "InitCatchPlace")]
        public static void InitCatchPlace_PostPatch()
        {
            if (!EnableFlag)
            {
                Clean();
                return;
            }
            //从UI_CatchCricket类取我们要的数据
            UI_CatchCricket uiCricket = UIElement.CatchCricket.UiBaseAs<UI_CatchCricket>();
            Traverse trv = Traverse.Create(uiCricket);
            //取地点信息列表
            UI_CatchCricket.CricketPlaceInfo[] placeList = trv.Field("_catchPlaceList").GetValue<UI_CatchCricket.CricketPlaceInfo[]>();
            //取RectTransform用来显示结果
            RectTransform rootRT = trv.Field("_catchPlaceRoot").GetValue<RectTransform>();

            for (int i = 0; i < 21; i++)
            {
                //挨个取蛐蛐信息
                UI_CatchCricket.CricketPlaceInfo info = placeList[i];
                CricketPartsItem colorConfig = CricketParts.Instance[info.CricketColorId];
                CricketPartsItem partConfig = CricketParts.Instance[info.CricketPartsId];
                //从cricketView里直接复制的代码 用来拼接蛐蛐名
                string CricketName = info.CricketPartsId > 0 ? ((colorConfig.NameOrder >= partConfig.NameOrder) ? (colorConfig.Name[0] + partConfig.Name[0]) : (partConfig.Name[0] + colorConfig.Name[0])) : colorConfig.Name[0];
                int CricketLevel = info.CricketPartsId > 0 ? Mathf.Max((int)colorConfig.Level, (int)partConfig.Level) : ((int)colorConfig.Level);
                //将名字赋上等级颜色字符
                string strNameWithColor = Extentions.SetGradeColor(CricketName, CricketLevel);

                //在地块区域上显示蛐蛐名
                var parent = rootRT.GetChild(i).transform;
                var textTrans = parent.Find("cricketShowName");
                if (textTrans == null)
                {
                    var text = GameObjectCreationUtils.UGUICreateTMPText(parent, strNameWithColor);
                    text.transform.gameObject.name = "cricketShowName";
                    text.fontSize = 25;
                    text.transform.localPosition = new Vector3(100, -100);
                }
                else
                {
                    textTrans.GetComponent<TMPro.TextMeshProUGUI>().text = strNameWithColor;
                }
            }
        }

        public static void Clean()
        {
            UI_CatchCricket uiCricket = UIElement.CatchCricket.UiBaseAs<UI_CatchCricket>();
            if (uiCricket != null)
            {
                Traverse trv = Traverse.Create(uiCricket);
                RectTransform rootRT = trv.Field("_catchPlaceRoot").GetValue<RectTransform>();
                for (int i = 0; i < 21; i++)
                {
                    var parent = rootRT.GetChild(i).transform;
                    var textTrans = parent.Find("cricketShowName");
                    if (textTrans != null)
                    {
                        GameObject.Destroy(textTrans.gameObject);
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UI_CatchCricket), "OnClickCatchPlace", new Type[] { typeof(int) }) ]
        public static void OnClickCatchPlace_PrePatch(int index)
        {
            if (!SetSingFlag)
            {
                return;
            }
            UI_CatchCricket uiCricket = UIElement.CatchCricket.UiBaseAs<UI_CatchCricket>();
            Traverse trv = Traverse.Create(uiCricket);
            UI_CatchCricket.CricketPlaceInfo[] placeList = trv.Field("_catchPlaceList").GetValue<UI_CatchCricket.CricketPlaceInfo[]>();
            placeList[index].SingLevel = 1000;
        }
    }
}
