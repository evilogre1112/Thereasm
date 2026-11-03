import re
from pathlib import Path

ROOT = Path(r"d:\Project\Thereasm")
PREFABS_DIR = ROOT / "Assets/Prefabs"

PREFAB_META = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def extract_tree_blocks(scene_content, root_rt_id):
    raw_blocks = re.findall(r'(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=\n--- !u!|\Z)', scene_content, re.DOTALL)
    records = {int(b[1]): (int(b[0]), b[2]) for b in raw_blocks}
    
    included_ids = set()
    
    def visit_rt(rt_id):
        included_ids.add(rt_id)
        rt_body = records[rt_id][1]
        m_go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', rt_body)
        if m_go:
            go_id = int(m_go.group(1))
            included_ids.add(go_id)
            go_body = records[go_id][1]
            comps = [int(x) for x in re.findall(r'component: \{fileID: (-?\d+)\}', go_body)]
            for c in comps:
                included_ids.add(c)
        
        m_ch = re.search(r'm_Children:(.*?)(?:m_Father|\Z)', rt_body, re.DOTALL)
        if m_ch:
            for ch_id in [int(x) for x in re.findall(r'- \{fileID: (-?\d+)\}', m_ch.group(1))]:
                visit_rt(ch_id)

    visit_rt(root_rt_id)
    return included_ids, records

def create_notepanel_prefab():
    scene_text = (ROOT / "Assets/Scenes/MainBoard.unity").read_text(encoding="utf-8")
    included_ids, records = extract_tree_blocks(scene_text, 1927024419)
    print(f"Extracted {len(included_ids)} objects for NotePanel.prefab")
    
    blocks_out = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:"]
    
    # Root RT 1927024419 must have m_Father: {fileID: 0}
    for obj_id in sorted(included_ids):
        t, body = records[obj_id]
        if obj_id == 1927024419:
            body = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: 0}', body)
        blocks_out.append(f"--- !u!{t} &{obj_id}\n{body.strip()}")
        
    prefab_path = PREFABS_DIR / "NotePanel.prefab"
    prefab_path.write_text("\n".join(blocks_out) + "\n", encoding="utf-8")
    
    meta_path = PREFABS_DIR / "NotePanel.prefab.meta"
    meta_path.write_text(PREFAB_META.replace("{guid}", "4c9a8101e45b46e3995f700000000010"), encoding="utf-8")
    print(f"Created {prefab_path.name}")

def create_noteslot_prefab():
    # Standalone NoteSlot prefab
    content = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &7000000000000000701
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000702}
  - component: {fileID: 7000000000000000703}
  - component: {fileID: 7000000000000000704}
  - component: {fileID: 7000000000000000705}
  m_Layer: 5
  m_Name: NoteSlot
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000702
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000701}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 96, y: 96}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000703
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000701}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000704
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000701}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000003, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &7000000000000000705
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000701}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ecb482457a208754c87fa3b63d3c87cd, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
"""
    prefab_path = PREFABS_DIR / "NoteSlot.prefab"
    prefab_path.write_text(content, encoding="utf-8")
    
    meta_path = PREFABS_DIR / "NoteSlot.prefab.meta"
    meta_path.write_text(PREFAB_META.replace("{guid}", "4c9a8101e45b46e3995f700000000012"), encoding="utf-8")
    print(f"Created {prefab_path.name}")

def create_noteblock_prefab():
    # Standalone NoteBlock prefab with child txt
    content = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &7000000000000000711
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000712}
  - component: {fileID: 7000000000000000713}
  - component: {fileID: 7000000000000000714}
  - component: {fileID: 7000000000000000715}
  m_Layer: 5
  m_Name: NoteBlock
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000712
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000711}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 7000000000000000722}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 86, y: 86}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000713
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000711}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000714
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000711}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000002, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &7000000000000000715
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000711}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 4300b0d85fd690844a62c3956c3f7571, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  originalParent: {fileID: 0}
  homeParent: {fileID: 0}
  clickSound: {fileID: 8300000, guid: 10595b1f51646364f9982df25e8465fa, type: 3}
--- !u!1 &7000000000000000721
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000722}
  - component: {fileID: 7000000000000000723}
  - component: {fileID: 7000000000000000724}
  m_Layer: 5
  m_Name: txt
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000722
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000721}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000712}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 86, y: 86}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000723
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000721}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000724
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000721}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: "1"
  m_isRightToLeft: 0
  m_fontAsset: {fileID: 11400000, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_sharedMaterial: {fileID: 3542882934977196910, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_fontSharedMaterials: []
  m_fontMaterial: {fileID: 0}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4291880439
  m_fontColor: {r: 0.972549, g: 0.92549, b: 0.835294, a: 1}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {r: 1, g: 1, b: 1, a: 1}
    topRight: {r: 1, g: 1, b: 1, a: 1}
    bottomLeft: {r: 1, g: 1, b: 1, a: 1}
    bottomRight: {r: 1, g: 1, b: 1, a: 1}
  m_fontColorGradientPreset: {fileID: 0}
  m_spriteAsset: {fileID: 0}
  m_tintAllSprites: 0
  m_StyleSheet: {fileID: 0}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 40
  m_fontSizeBase: 40
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 18
  m_fontSizeMax: 72
  m_fontStyle: 0
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 0
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {fileID: 0}
  parentLinkedComponent: {fileID: 0}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {x: 0, y: 0, z: 0, w: 0}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {fileID: 0}
  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}
"""
    prefab_path = PREFABS_DIR / "NoteBlock.prefab"
    prefab_path.write_text(content, encoding="utf-8")
    
    meta_path = PREFABS_DIR / "NoteBlock.prefab.meta"
    meta_path.write_text(PREFAB_META.replace("{guid}", "4c9a8101e45b46e3995f700000000011"), encoding="utf-8")
    print(f"Created {prefab_path.name}")

if __name__ == "__main__":
    create_notepanel_prefab()
    create_noteslot_prefab()
    create_noteblock_prefab()
    print("All prefabs generated successfully!")
