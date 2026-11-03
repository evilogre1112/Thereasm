using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GameLocalizationValidation
{
    const string Request="Thereasm.LocalizationValidation";
    static int assertions;
    static TMP_Text animated;
    static float firstY;
    static double motionStart;
    static int motionPhase;
    static GameLocalization testedCatalog;
    [Serializable] class Report { public bool passed; public int assertions,entries; public string message; }
    static GameLocalizationValidation()
    {EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Request,false))EditorApplication.delayCall+=Check;};}
    public static void Run(){SessionState.SetBool(Request,true);EditorSceneManager.OpenScene("Assets/Scenes/MenuScenes.unity");EditorApplication.isPlaying=true;}
    static void Expect(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static void Set(object target,string property,object value)=>target.GetType().GetProperty(property).SetValue(target,value);
    static void Check()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<GameLocalization>("Assets/UI/Localization/GameLocalization.asset");
        try
        {
            Expect(catalog!=null&&catalog.defaultLanguage==GameLanguage.English,"English is not the default language.");
            Localizer.Initialize(catalog,GameLanguage.English);
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                Localizer.SetLanguage(language,false);
                foreach(var entry in catalog.entries)
                {
                    if(entry.key.StartsWith("card.name.")) Expect(entry.vietnamese==entry.english,"A card name was translated: "+entry.key);
                    if(entry.key.ToLowerInvariant().Contains("description"))
                        foreach(var name in new[]{"Quản trò","Quản Trò","Giáo sĩ","Thợ săn","Trưởng lão","Thám tử","Phù thủy"})
                            Expect(!entry.vietnamese.Contains(name),"Vietnamese card name inside a description: "+entry.key+" / "+name);
                    string expected=language==GameLanguage.English?entry.english:entry.vietnamese;
                    Expect(!string.IsNullOrWhiteSpace(expected),"Missing translation: "+entry.key);
                    if(!entry.source.Contains("{"))Expect(Localizer.T(entry.source)==expected,"Wrong translation: "+entry.key);
                    string printed=System.Text.RegularExpressions.Regex.Replace(expected,@"\{\d+\}|<[^>]+>","");
                    foreach(char c in printed.Where(c=>!char.IsControl(c)).Distinct())
                        Expect(catalog.readableTmpFont.HasCharacter(c),"Missing font glyph U+"+((int)c).ToString("X4")+" in "+entry.key);
                }
            }
            Localizer.SetLanguage(GameLanguage.English,false);
            string missing=Localizer.T("Chưa bỏ phiếu: Villager (số 10), Villager (số 11).");
            Expect(missing.Contains("Still to vote:")&&missing.Contains("number 10")&&missing.Contains("number 11")&&!missing.Contains("số"),"Dynamic missing-vote feedback is not fully localized.");
            Expect(Localizer.T("Đã bỏ phiếu 2/3 · Kéo từ mỗi lá của bạn tới một lá khác trên bàn.").StartsWith("Voted 2/3"),"Dynamic vote progress was not translated.");
            var session=OnlineSession.Instance;
            if(session==null)session=new GameObject("localization session").AddComponent<OnlineSession>();
            session.enabled=false;Set(session,"Connected",true);Set(session,"MatchStarted",true);Set(session,"IsHost",true);
            var role=typeof(OnlineSession).GetField("localRole",BindingFlags.Instance|BindingFlags.NonPublic);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardUI.prefab");
            var parent=GameStartUI.Current.GetComponent<RectTransform>();
            var data=AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Villager10.asset");
            var card=Object.Instantiate(prefab,parent).GetComponent<CardInstance>();card.enabled=false;card.SetupCard(data);
            var view=card.GetComponent<CardOwnershipView>();
            var interaction=card.GetComponent<CardInteraction>();
            var dialogue=card.GetComponent<CardDialogue>();
            interaction.descriptionPanel.SetActive(true);dialogue.bubbleObject.SetActive(true);
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            {
                Localizer.SetLanguage(language,false);
                foreach(string guid in AssetDatabase.FindAssets("t:CardData",new[]{"Assets/ScriptableObjects"}))
                {
                    var definition=AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach(string description in new[]{definition.description,definition.curDescription,definition.infectedDescription,definition.curInfectedDescription})
                    {
                        if(string.IsNullOrEmpty(description))continue;
                        interaction.SetDescription(description);
                        Expect(interaction.descriptionText.text==Localizer.T(description),"Inspection path bypassed translation: "+definition.name);
                        interaction.descriptionText.ForceMeshUpdate();
                        Expect(!interaction.descriptionText.isTextOverflowing,"Clipped "+language+" description: "+definition.name+" / "+description);
                    }
                    if(definition.quotes==null)continue;
                    foreach(string quote in definition.quotes)
                    {
                        dialogue.tmpText.text=Localizer.T(quote);dialogue.tmpText.GetComponent<LocalizedLabel>().Refresh();dialogue.tmpText.ForceMeshUpdate();
                        Expect(!dialogue.tmpText.isTextOverflowing,"Clipped "+language+" popup: "+definition.name+" / "+quote);
                    }
                }
            }
            interaction.descriptionPanel.SetActive(false);dialogue.bubbleObject.SetActive(false);
            // Exercise the independent draft prefab and the actual language button, not just a manual text refresh.
            var draft=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DraftCard.prefab"),parent).GetComponent<CardInteraction>();
            draft.enabled=false;draft.descriptionPanel.SetActive(true);
            var controller=GameStartUI.Current.GetComponent<LanguageController>();
            var trader=AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Trader20.asset");
            Localizer.SetLanguage(GameLanguage.English,false);draft.SetDescription(trader.description);
            Expect(draft.descriptionText.text==Localizer.T(trader.description),"Draft did not translate on creation.");
            string preference="thereasm.language.English";bool hadPreference=PlayerPrefs.HasKey(preference);int remembered=PlayerPrefs.GetInt(preference);
            try
            {
                controller.Vietnamese();
                Expect(draft.descriptionText.text==Localizer.T(trader.description)&&draft.descriptionText.text!=trader.description,"Changing to Vietnamese left the draft description in English.");
                controller.English();
                Expect(draft.descriptionText.text==Localizer.T(trader.description),"Draft did not switch back to English.");
            }
            finally{if(hadPreference)PlayerPrefs.SetInt(preference,remembered);else PlayerPrefs.DeleteKey(preference);PlayerPrefs.Save();}
            view.Refresh();Expect(!view.badgeBackground.gameObject.activeSelf,"Hand cards still repeat ownership banners.");
            interaction.isPlaced=true;view.Refresh();Expect(view.badgeBackground.gameObject.activeSelf,"Board ownership stamp is missing.");
            interaction.isPlaced=false;
            var visual=(RectTransform)interaction.visualChild;
            Expect(dialogue.bubbleObject.transform.IsChildOf(visual),"Dialogue does not follow the visible card.");
            var speechRect=(RectTransform)dialogue.bubbleObject.transform;
            Expect(Mathf.Abs(speechRect.anchoredPosition.x)<.01f,"Dialogue is shifted sideways relative to the card.");
            var cardRect=(RectTransform)card.transform;Vector3 oldPosition=cardRect.position,oldScale=cardRect.localScale;
            interaction.descriptionPanel.SetActive(true);cardRect.localScale=Vector3.one*interaction.inspectScaleMultiplier;cardRect.position=interaction.InspectionPosition();
            Canvas.ForceUpdateCanvases();
            var cardCorners=new Vector3[4];var descriptionCorners=new Vector3[4];
            cardRect.GetWorldCorners(cardCorners);((RectTransform)interaction.descriptionPanel.transform).GetWorldCorners(descriptionCorners);
            float groupLeft=Mathf.Min(cardCorners.Min(c=>c.x),descriptionCorners.Min(c=>c.x));
            float groupRight=Mathf.Max(cardCorners.Max(c=>c.x),descriptionCorners.Max(c=>c.x));
            Expect(Mathf.Abs((groupLeft+groupRight)*.5f-card.transform.root.position.x)<1,"Inspection card and text are not centered together.");
            interaction.descriptionPanel.SetActive(false);cardRect.position=oldPosition;cardRect.localScale=oldScale;
            Object.Destroy(draft.gameObject);
            foreach(var language in new[]{GameLanguage.English,GameLanguage.Vietnamese})
            foreach(var owner in new[]{DefaultOwner.Player1,DefaultOwner.Player2})
            {
                Localizer.SetLanguage(language,false);role.SetValue(session,owner);view.Refresh();
                string expected=Localizer.T(owner==data.owner?"CỦA BẠN":"ĐỐI THỦ");
                Expect(view.ownerLabel.text==expected,"Ownership label does not follow the local player's public faction.");
                Color before=view.frame.effectColor;string badge=view.ownerLabel.text;
                card.isInfected=true;card.cardImage.sprite=data.cursemancerArtwork;view.Refresh();
                Expect(view.ownerLabel.text==badge&&view.frame.effectColor==before,"Ownership badge disclosed a hidden role or infection.");
                card.isInfected=false;card.cardImage.sprite=data.defaultArtwork;view.Refresh();
                Expect(view.ownerLabel.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Ownership badge intercepts card input.");
                Expect(card.GetComponentsInChildren<Transform>(true).All(t=>t.name!="Localized Printed Name"&&t.name!="Localized printed name"),"A UI overlay still covers the original English card name.");
            }
            var neutral=AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/Trader20.asset");card.SetupCard(neutral);
            card.GetComponent<CardInteraction>().isPlaced=true;card.lastPlacedBy=OnlineSession.LocalRole;
            Localizer.SetLanguage(GameLanguage.English,false);view.Refresh();Expect(view.ownerLabel.text=="NEUTRAL","Neutral card is incorrectly labelled as belonging to a player.");
            foreach(var label in Object.FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Include,FindObjectsSortMode.None))label.Refresh();
            Expect(GameStartUI.Current.GetComponent<LanguageController>()!=null,"Menu is missing its saved language selector.");
            var bubble=card.GetComponent<CardDialogue>();bubble.bubbleObject.SetActive(true);
            animated=bubble.tmpText;animated.text="The shadows are moving.";
            var localized=animated.GetComponent<LocalizedLabel>();localized.Refresh();
            var effect=animated.GetComponent<FloatingUI>();Expect(effect!=null&&effect.enabled&&effect.wobbleAmount>0,"Existing wavy text effect was removed or disabled.");
            if(localized.originalTextMaterial!=null)
            {
                Expect(animated.fontSharedMaterial.shader==localized.originalTextMaterial.shader,"Original text-effect shader was replaced.");
                foreach(string property in new[]{"_OutlineWidth","_GlowPower","_UnderlaySoftness"})
                    if(localized.originalTextMaterial.HasProperty(property))Expect(Mathf.Approximately(animated.fontSharedMaterial.GetFloat(property),localized.originalTextMaterial.GetFloat(property)),"Text material effect changed: "+property);
            }
            testedCatalog=catalog;motionPhase=0;motionStart=EditorApplication.timeSinceStartup;firstY=float.NaN;
            EditorApplication.update+=CheckMotion;
        }
        catch(Exception exception){Finish(false,exception.ToString(),catalog!=null?catalog.entries.Length:0);}
    }
    static void CheckMotion()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-motionStart>6)throw new Exception("Wavy text stopped moving after font/language changes.");
            if(animated==null||animated.textInfo.characterCount==0||animated.textInfo.meshInfo.Length==0)return;
            float y=animated.textInfo.meshInfo[0].vertices[0].y;
            if(float.IsNaN(firstY)){firstY=y;return;}
            if(EditorApplication.timeSinceStartup-motionStart<.3||Mathf.Abs(y-firstY)<.03f)return;
            Expect(true,"Wavy text moves in "+Localizer.Language);
            if(motionPhase++==0)
            {
                Localizer.SetLanguage(GameLanguage.Vietnamese,false);animated.text="Bóng tối đang chuyển động.";animated.GetComponent<LocalizedLabel>().Refresh();
                firstY=float.NaN;motionStart=EditorApplication.timeSinceStartup;return;
            }
            EditorApplication.update-=CheckMotion;
            Finish(true,"Bilingual text, Vietnamese glyphs, public ownership, preserved material effects and animated text in both languages passed.",testedCatalog.entries.Length);
        }
        catch(Exception exception){EditorApplication.update-=CheckMotion;Finish(false,exception.ToString(),testedCatalog.entries.Length);}
    }
    static void Finish(bool passed,string message,int entries)
    {
        SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"GameLocalizationValidationResult.json"),JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,entries=entries,message=message},true));
        Debug.Log("LOCALIZATION RESULT: "+message);EditorApplication.Exit(passed?0:1);
    }
}
