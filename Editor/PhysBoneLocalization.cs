using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarChamber.PhysBoneMotion
{
    public enum PreviewLanguage
    {
        Chinese = 0,
        English = 1,
        Japanese = 2,
        Korean = 3
    }

    /// <summary>
    /// Lightweight editor localization. Chinese is the source language and the fallback;
    /// every other language only needs an entry for strings that should be translated.
    /// Entries live in code so the package has no extra asset or package dependency.
    /// </summary>
    public static class PhysBoneLocalization
    {
        private const string LanguageKey = "AvatarChamber.PhysBoneMotion.Language";

        public static readonly string[] LanguageNames =
        {
            "中文",
            "English",
            "日本語",
            "한국어"
        };

        public static event Action LanguageChanged;

        private static PreviewLanguage _language;
        private static bool _languageLoaded;

        public static PreviewLanguage Language
        {
            get
            {
                if (!_languageLoaded)
                {
                    var value = EditorPrefs.GetInt(LanguageKey, 0);
                    _language = (PreviewLanguage)Mathf.Clamp(value, 0, LanguageNames.Length - 1);
                    _languageLoaded = true;
                }

                return _language;
            }
            set
            {
                var index = Mathf.Clamp((int)value, 0, LanguageNames.Length - 1);
                if (_languageLoaded && (int)_language == index)
                {
                    return;
                }

                _language = (PreviewLanguage)index;
                _languageLoaded = true;
                EditorPrefs.SetInt(LanguageKey, index);
                if (LanguageChanged != null)
                {
                    LanguageChanged();
                }
            }
        }

        public static string Tr(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return source ?? string.Empty;
            }

            var language = Language;
            if (language == PreviewLanguage.Chinese)
            {
                return source;
            }

            Entry entry;
            if (!Entries.TryGetValue(source, out entry) || entry == null)
            {
                return source;
            }

            var translated = language == PreviewLanguage.English
                ? entry.English
                : language == PreviewLanguage.Japanese
                    ? entry.Japanese
                    : entry.Korean;

            return string.IsNullOrEmpty(translated) ? source : translated;
        }

        public static string Format(string sourceFormat, params object[] args)
        {
            return string.Format(Tr(sourceFormat), args);
        }

        public static GUIContent Content(string text, string tooltip = null)
        {
            var content = new GUIContent(Tr(text));
            if (!string.IsNullOrEmpty(tooltip))
            {
                content.tooltip = Tr(tooltip);
            }

            return content;
        }

        public static string[] TrArray(string[] source)
        {
            if (source == null)
            {
                return new string[0];
            }

            var result = new string[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = Tr(source[i]);
            }

            return result;
        }

        private sealed class Entry
        {
            public readonly string English;
            public readonly string Japanese;
            public readonly string Korean;

            public Entry(string english, string japanese, string korean)
            {
                English = english;
                Japanese = japanese;
                Korean = korean;
            }
        }

        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>
            {
                // Window, tabs, common controls.
                { "PhysBone 动态", new Entry("PhysBone Motion", "PhysBone モーション", "PhysBone 모션") },
                { "预览", new Entry("Preview", "プレビュー", "미리보기") },
                { "动骨", new Entry("PhysBone", "動骨", "피직본") },
                { "设置", new Entry("Settings", "設定", "설정") },
                { "目标动骨", new Entry("Target PhysBone", "対象 PhysBone", "대상 피직본") },
                { "当前角色上没有 PhysBone。", new Entry("No PhysBone on the current avatar.", "現在のアバターに PhysBone がありません。", "현재 아바타에 PhysBone이 없습니다.") },
                { "当前角色没有 PhysBone，或选择不属于角色。", new Entry("The current avatar has no PhysBone, or the selection does not belong to it.", "現在のアバターに PhysBone がないか、選択がアバターに属していません。", "현재 아바타에 PhysBone이 없거나 선택한 대상이 아바타에 속하지 않습니다.") },
                { "当前角色没有 PhysBone，无法显示目标动骨和驱动对象设置。", new Entry("The current avatar has no PhysBone, so Target PhysBone and Driver settings are unavailable.", "現在のアバターに PhysBone がないため、対象 PhysBone と駆動対象の設定を表示できません。", "현재 아바타에 PhysBone이 없어 대상 피직본 및 구동 대상 설정을 표시할 수 없습니다.") },
                { "当前动骨：{0}", new Entry("Current PhysBone: {0}", "現在の PhysBone：{0}", "현재 PhysBone: {0}") },
                { "请选择角色上的任意物体（骨骼、网格、Avatar 根节点都可以）。", new Entry("Select any part of the avatar (bone, mesh, or avatar root).", "アバターの任意の部分（ボーン、メッシュ、アバターのルート）を選択してください。", "아바타의 아무 부분(본, 메시, 아바타 루트)을 선택하세요.") },
                { "未检测到 VRChat SDK（VRCPhysBone）。请先在此工程安装 VRChat Avatars SDK。", new Entry("VRChat SDK (VRCPhysBone) not detected. Install VRChat Avatars SDK in this project first.", "VRChat SDK（VRCPhysBone）が見つかりません。先に VRChat Avatars SDK を導入してください。", "VRChat SDK(VRCPhysBone)를 찾을 수 없습니다. 먼저 이 프로젝트에 VRChat Avatars SDK를 설치하세요.") },
                { "物理预览", new Entry("Physics Preview", "物理プレビュー", "물리 미리보기") },
                { "预览只修改 Play Mode 中的 Transform，退出 Play Mode 后自动复原，不会写入场景或预制体。", new Entry("Preview only changes Transforms in Play Mode. They restore automatically when Play Mode ends and are never written to the scene or prefab.", "プレビューは Play Mode 中の Transform のみ変更します。終了時に自動復元され、シーンやプレハブには書き込まれません。", "미리보기는 Play Mode 중 Transform만 변경합니다. 종료 시 자동 복원되며 씬이나 프리팹에 기록되지 않습니다.") },
                { "物理预览已激活", new Entry("Physics preview active", "物理プレビュー有効", "물리 미리보기 활성화됨") },
                { "物理预览未激活（点击动作进入运行模式）", new Entry("Physics preview inactive (click a preset to enter Play Mode)", "物理プレビュー無効（プリセットをクリックして再生モードへ）", "물리 미리보기 비활성화 (프리셋을 클릭하여 실행 모드로 진입)") },
                { "状态：可随时保存更改", new Entry("Status: changes can be saved at any time", "状態：いつでも変更を保存できます", "상태: 언제든지 변경 사항을 저장할 수 있습니다") },
                { "状态：编辑模式", new Entry("Status: Edit Mode", "状態：編集モード", "상태: 편집 모드") },
                { "显示骨骼", new Entry("Show Bones", "ボーンを表示", "본 표시") },
                { "高亮动骨", new Entry("Highlight PhysBones", "動骨をハイライト", "피직본 하이라이트") },
                { "加粗显示带 VRCPhysBone 的骨骼链；点击骨骼链上的圆点可直接选中对应骨骼。", new Entry("Draws PhysBone chains with thicker lines; click a joint dot to select that bone directly.", "VRCPhysBone のチェーンを太い線で表示します。関節の点をクリックすると、そのボーンを直接選択できます。", "VRCPhysBone 체인을 굵은 선으로 표시합니다. 관절 점을 클릭하면 해당 본을 바로 선택할 수 있습니다.") },
                { "重新居中 Gizmo", new Entry("Recenter Gizmo", "Gizmo を中央に", "Gizmo 재중앙") },
                { "在鼠标下方放置 Gizmo", new Entry("Place Gizmo Under Mouse", "マウス位置に Gizmo を配置", "마우스 아래에 Gizmo 배치") },
                { "设置放置快捷键...", new Entry("Set Placement Shortcut...", "配置ショートカット...", "배치 단축키 설정...") },
                { "默认 X：在 Scene 视图把中心移到鼠标下方。", new Entry("Default X: center the Scene view under the mouse.", "既定の X：Scene ビューの中心をマウス位置へ移動。", "기본 X: Scene 뷰 중심을 마우스 아래로 이동.") },
                { "显示角色操纵杆", new Entry("Show Avatar Gizmo", "アバターギズモを表示", "아바타 기즈모 표시") },
                { "在角色中心显示移动箭头和旋转圆环；选中任意部件时也能拖动整个模型，且不会改变当前选择。", new Entry("Shows translation arrows and rotation rings at the avatar center. You can manipulate the whole model while keeping any part selected, and the selection does not change.", "アバター中心に移動矢印と回転リングを表示します。任意のパーツを選択したままモデル全体を操作でき、選択は変わりません。", "아바타 중심에 이동 화살표와 회전 링을 표시합니다. 어떤 부품을 선택한 상태에서도 모델 전체를 조작할 수 있으며 선택은 바뀌지 않습니다.") },
                { "拖动箭头移动整个角色，拖动圆环旋转；Play Mode 中可实时观察 PhysBone 的惯性与链式联动。", new Entry("Drag the arrows to move the whole avatar and the rings to rotate it. In Play Mode, watch PhysBone inertia and chain motion in real time.", "矢印でアバター全体を移動し、リングで回転します。Play Mode 中は PhysBone の慣性とチェーンの動きをリアルタイムで確認できます。", "화살표로 아바타 전체를 이동하고 링으로 회전합니다. Play Mode에서는 PhysBone의 관성과 체인 움직임을 실시간으로 확인할 수 있습니다.") },
                { "设置快捷键...", new Entry("Set Shortcut...", "ショートカットを設定...", "단축키 설정...") },
                { "请按任意按键...", new Entry("Press any key...", "任意のキーを押してください...", "아무 키나 누르세요...") },
                { "按任意按键完成绑定；按 Esc 取消。", new Entry("Press any key to bind; press Esc to cancel.", "任意のキーで設定。Esc でキャンセル。", "아무 키나 눌러 설정하고, Esc로 취소합니다.") },
                { "点击后按任意按键，即可绑定“在鼠标下方放置 Gizmo”的快捷键。", new Entry("Click, then press any key to bind the Place Gizmo Under Mouse shortcut.", "クリック後に任意のキーを押すと、「マウス下にギズモを配置」のショートカットを設定できます。", "클릭한 뒤 아무 키나 누르면 '마우스 아래에 기즈모 배치' 단축키를 설정합니다.") },
                { "放置快捷键已设为 {0}", new Entry("Placement shortcut set to {0}", "配置ショートカットを {0} に設定しました", "배치 단축키를 {0}(으)로 설정했습니다") },
                { "快捷键：{0} — 在鼠标下方放置 Gizmo", new Entry("Shortcut: {0} — place the gizmo under the mouse", "ショートカット：{0} — マウス下にギズモを配置", "단축키: {0} — 마우스 아래에 기즈모 배치") },
                { "快捷键设置失败，请重试。", new Entry("Failed to set the shortcut. Please try again.", "ショートカットの設定に失敗しました。もう一度お試しください。", "단축키 설정에 실패했습니다. 다시 시도하세요.") },
                { "保存更改", new Entry("Save Changes", "変更を保存", "변경 사항 저장") },
                { "保存并重载", new Entry("Save & Reload", "保存して再読み込み", "저장 후 다시 로드") },
                { "保存本次 Play Mode 中修改过的所有 PhysBone。", new Entry("Save every PhysBone modified during this Play Mode session.", "この Play Mode 中に変更したすべての PhysBone を保存します。", "이번 Play Mode에서 수정한 모든 PhysBone을 저장합니다.") },
                { "保存所有改动，退出 Play Mode 后立即重新进入，便于重新测试。", new Entry("Save all changes, exit Play Mode, then re-enter immediately to test again.", "すべての変更を保存し、Play Mode を終了してすぐに再入し、再テストします。", "모든 변경 사항을 저장한 뒤 Play Mode를 종료하고 즉시 다시 들어가 테스트합니다.") },
                { "已保存修改，正在重新进入 Play Mode。", new Entry("Changes saved; re-entering Play Mode.", "変更を保存しました。Play Mode に再入します。", "변경 사항을 저장했습니다. Play Mode에 다시 들어갑니다.") },
                { "保存并退出", new Entry("Save & Exit", "保存して終了", "저장 후 종료") },
                { "退出预览", new Entry("Exit Preview", "プレビューを終了", "미리보기 종료") },
                { "已暂存参数，退出 Play Mode 后会写回场景。", new Entry("Parameters staged; they will be written back after exiting Play Mode.", "パラメータを一時保存しました。Play Mode 終了後にシーンへ書き戻します。", "매개변수가 임시 저장되었습니다. Play Mode 종료 후 씬에 기록됩니다.") },
                { "在 Inspector 中选中当前 PhysBone", new Entry("Select Current PhysBone in Inspector", "現在の PhysBone を Inspector で選択", "현재 PhysBone을 Inspector에서 선택") },
                { "使用 Inspector 修改物理组件。", new Entry("Edit the physics component in the Inspector.", "Inspector で物理コンポーネントを編集します。", "Inspector에서 물리 컴포넌트를 편집합니다.") },
                { "动骨参数", new Entry("PhysBone Parameters", "動骨パラメータ", "피직본 매개변수") },
                { "pull / spring / stiffness 都为 0，动骨可能不会摆动。", new Entry("pull / spring / stiffness are all 0; the PhysBone may not move.", "pull / spring / stiffness がすべて 0 のため、動骨が揺れない可能性があります。", "pull / spring / stiffness가 모두 0이라 피직본이 흔들리지 않을 수 있습니다.") },
                { "该角色没有 PhysBone，预设动作不可用；仍可调整动作参数。", new Entry("This avatar has no PhysBone. Presets are unavailable, but motion parameters can still be adjusted.", "このアバターには PhysBone がありません。プリセットは使用できませんが、モーションパラメータは調整できます。", "이 아바타에는 PhysBone이 없습니다. 프리셋은 사용할 수 없지만 모션 매개변수는 조정할 수 있습니다.") },

                // Motion parameters and presets.
                { "动作参数", new Entry("Motion Parameters", "モーションパラメータ", "모션 매개변수") },
                { "运动方式", new Entry("Motion Mode", "動作方式", "동작 방식") },
                { "位移：整体平移；旋转：整体转动", new Entry("Translation: move the whole object; Rotation: rotate the whole object.", "移動：全体を平行移動。回転：全体を回転。", "이동: 전체를 평행 이동, 회전: 전체를 회전.") },
                { "位移（平移）", new Entry("Translation", "移動（平行移動）", "이동(평행 이동)") },
                { "旋转", new Entry("Rotation", "回転", "회전") },
                { "幅度（米）", new Entry("Amplitude (m)", "振幅（m）", "진폭(m)") },
                { "整体移动的距离", new Entry("Distance to move the whole object.", "全体を移動する距離。", "전체를 이동하는 거리.") },
                { "幅度（角度）", new Entry("Amplitude (deg)", "振幅（度）", "진폭(도)") },
                { "整体旋转的角度", new Entry("Angle to rotate the whole object.", "全体を回転させる角度。", "전체를 회전하는 각도.") },
                { "频率（Hz）", new Entry("Frequency (Hz)", "周波数（Hz）", "주파수(Hz)") },
                { "每秒摆动次数", new Entry("Swings per second.", "1 秒あたりの揺れ回数。", "초당 흔들림 횟수.") },
                { "时长（秒）", new Entry("Duration (s)", "時間（秒）", "지속 시간(초)") },
                { "0 表示循环播放，直到手动停止", new Entry("0 loops until stopped manually.", "0 で手動停止までループ。", "0은 수동으로 멈출 때까지 반복.") },
                { "0 = 循环播放", new Entry("0 = Loop", "0 = ループ", "0 = 반복") },
                { "预设动作", new Entry("Presets", "プリセット", "프리셋") },
                { "上下摆动", new Entry("Bob", "上下揺れ", "상하 흔들림") },
                { "左右摇摆", new Entry("Sway", "左右揺れ", "좌우 흔들림") },
                { "前后晃动", new Entry("Surge", "前後揺れ", "전후 흔들림") },
                { "画圈", new Entry("Circle", "円運動", "원운동") },
                { "高频抖动", new Entry("Jitter", "高周波振動", "고주파 떨림") },
                { "风吹", new Entry("Wind", "風", "바람") },
                { "脉冲回弹", new Entry("Pulse", "パルス", "펄스") },
                { "播放状态", new Entry("Playback Status", "再生状態", "재생 상태") },
                { "播放中：{0}", new Entry("Playing: {0}", "再生中：{0}", "재생 중: {0}") },
                { "（剩余 {0} 秒）", new Entry(" ({0}s remaining)", "（残り {0} 秒）", " (남은 시간 {0}초)") },
                { "停止并复原", new Entry("Stop & Restore", "停止して復元", "중지 및 복원") },
                { "当前在 Play Mode 中，选择一个预设动作开始预览。", new Entry("Currently in Play Mode. Select a preset to start previewing.", "現在 Play Mode 中です。プリセットを選択してプレビューを開始してください。", "현재 Play Mode 중입니다. 프리셋을 선택하여 미리보기를 시작하세요.") },
                { "限位在 Play Mode 中实时参与解算；限位范围由 VRChat SDK 的 Gizmo 显示，可在「动骨 > 选项」打开「显示 Gizmo」并调高「限位透明度」。", new Entry("Limits are solved in Play Mode in real time. The limit range is drawn by the VRChat SDK gizmo; enable Show Gizmos and raise Limit Opacity under PhysBone > Options.", "リミットは Play Mode 中にリアルタイムで解算されます。リミット範囲は VRChat SDK の Gizmo で表示され、「動骨 > オプション」で「Gizmo を表示」を有効にし、「リミットの不透明度」を上げると確認できます。", "제한은 Play Mode에서 실시간으로 계산됩니다. 제한 범위는 VRChat SDK 기즈모로 표시되며, '피직본 > 옵션'에서 'Gizmo 표시'를 켜고 '제한 불투명도'를 높이면 확인할 수 있습니다.") },
                { "当前限位类型为 None，播放时不会应用角度限制。", new Entry("The current limit type is None, so angle limits are not applied during playback.", "現在のリミットタイプは None のため、再生中に角度リミットは適用されません。", "현재 제한 유형이 None이므로 재생 중 각도 제한이 적용되지 않습니다.") },
                { "当前限位：{0}（X {1:0}° / Z {2:0}°）", new Entry("Current limit: {0} (X {1:0}° / Z {2:0}°)", "現在のリミット：{0}（X {1:0}° / Z {2:0}°）", "현재 제한: {0} (X {1:0}° / Z {2:0}°)") },
                { "当前是编辑模式；点击任意预设动作会自动进入 Play Mode。", new Entry("Currently in Edit Mode. Clicking any preset enters Play Mode automatically.", "現在は編集モードです。プリセットをクリックすると自動的に Play Mode へ入ります。", "현재 편집 모드입니다. 프리셋을 클릭하면 자동으로 Play Mode에 들어갑니다.") },
                { "播放序列 {0}/{1}：{2}", new Entry("Playlist {0}/{1}: {2}", "再生リスト {0}/{1}：{2}", "재생 목록 {0}/{1}: {2}") },
                { "播放动画：{0}", new Entry("Playing clip: {0}", "アニメーション再生：{0}", "애니메이션 재생: {0}") },

                // Target PhysBone and driver.
                { "PhysBone", new Entry("PhysBone", "PhysBone", "PhysBone") },
                { "当前预览的 VRCPhysBone，可直接拖入或从对象选择器切换。", new Entry("The VRCPhysBone being previewed. Drag one in or pick it from the object selector.", "プレビュー中の VRCPhysBone。ドラッグするかオブジェクトセレクターから切り替えます。", "미리보기 중인 VRCPhysBone입니다. 드래그하거나 오브젝트 선택기에서 전환하세요.") },
                { "层级路径", new Entry("Hierarchy Path", "階層パス", "계층 경로") },
                { "根据当前 PhysBone 自动生成，不能直接编辑；可以选中复制。", new Entry("Generated from the current PhysBone; read-only, but you can select and copy it.", "現在の PhysBone から自動生成。直接編集はできませんが、選択してコピーできます。", "현재 PhysBone에서 자동 생성됩니다. 직접 편집할 수는 없지만 선택하여 복사할 수 있습니다.") },
                { "根骨骼", new Entry("Root Bone", "ルートボーン", "루트 본") },
                { "这条 PhysBone 链条的起点；留空时默认使用组件所在节点。修改后会改变链长和受控骨骼。", new Entry("Start of this PhysBone chain. If empty, the component's own node is used. Changing it alters the chain length and controlled bones.", "この PhysBone チェーンの起点。空の場合はコンポーネントのノードを使用。変更するとチェーン長と制御ボーンが変わります。", "이 PhysBone 체인의 시작점입니다. 비워 두면 컴포넌트가 있는 노드를 사용합니다. 변경하면 체인 길이와 제어 본이 달라집니다.") },
                { "链长（估算）", new Entry("Chain Length (est.)", "チェーン長（推定）", "체인 길이(추정)") },
                { "{0} 节", new Entry("{0} joints", "{0} 節", "{0} 관절") },
                { "（到 {0} 为止，它有自己的 PhysBone）", new Entry(" (up to {0}, which has its own PhysBone)", "（{0} まで。独自の PhysBone あり）", " ({0}까지, 자체 PhysBone 있음)") },
                { "该 PhysBone 组件处于禁用状态，预览不会产生效果。", new Entry("This PhysBone component is disabled; the preview will have no effect.", "この PhysBone コンポーネントは無効です。プレビューは効果がありません。", "이 PhysBone 컴포넌트가 비활성화되어 미리보기 효과가 없습니다.") },
                { "请选择场景中、属于角色的 VRCPhysBone。", new Entry("Select a VRCPhysBone in the scene that belongs to the avatar.", "シーン内でアバターに属する VRCPhysBone を選択してください。", "씬에서 아바타에 속한 VRCPhysBone을 선택하세요.") },
                { "根骨骼必须是当前角色层级内的场景对象。", new Entry("The root bone must be a scene object inside the current avatar hierarchy.", "ルートボーンは現在のアバター階層内のシーンオブジェクトである必要があります。", "루트 본은 현재 아바타 계층 내의 씬 오브젝트여야 합니다.") },
                { "驱动对象", new Entry("Driver", "駆動対象", "구동 대상") },
                { "选择预览时移动的对象。推荐整个模型；直接驱动动骨根节点可能不自然。", new Entry("Choose what moves during preview. Whole avatar is recommended; driving the PhysBone root directly may look unnatural.", "プレビュー時に動かす対象を選択します。モデル全体を推奨。動骨ルートの直接駆動は不自然になる場合があります。", "미리보기 중 이동할 대상을 선택합니다. 전체 모델을 권장하며, 피직본 루트를 직접 구동하면 부자연스러울 수 있습니다.") },
                { "驱动", new Entry("Driver", "駆動", "구동") },
                { "推荐：移动整个 Avatar，PhysBone 会像手动移动主物体一样自然解算，限位也会生效。", new Entry("Recommended: move the whole avatar. PhysBones resolve naturally and limits apply, just like moving the main object by hand.", "推奨：アバター全体を移動。手でメインオブジェクトを動かすのと同じように PhysBone が自然に解算され、リミットも有効になります。", "권장: 아바타 전체를 이동합니다. 메인 오브젝트를 직접 움직일 때처럼 PhysBone이 자연스럽게 계산되고 제한도 적용됩니다.") },
                { "直接驱动动骨根节点可能产生不自然的抽搐，限位表现也与整体移动不同。", new Entry("Driving the PhysBone root directly may cause unnatural twitching, and limits behave differently from moving the whole avatar.", "動骨ルートを直接駆動すると不自然な痙攣が起きる可能性があり、リミットの挙動も全体移動とは異なります。", "피직본 루트를 직접 구동하면 부자연스러운 떨림이 발생할 수 있고, 제한 동작도 전체 이동과 다릅니다.") },
                { "驱动动骨根节点的父级骨骼，适合头发、耳朵这类挂在头部的链条。", new Entry("Drive the parent bone of the PhysBone root; suitable for hair or ears attached to the head.", "動骨ルートの親ボーンを駆動。頭部に接続された髪や耳などに適しています。", "피직본 루트의 부모 본을 구동합니다. 머리에 연결된 머리카락이나 귀에 적합합니다.") },
                { "驱动一个自定义 Transform。", new Entry("Drive a custom Transform.", "カスタム Transform を駆動します。", "사용자 지정 Transform을 구동합니다.") },
                { "自定义", new Entry("Custom", "カスタム", "사용자 지정") },
                { "当前驱动：{0}", new Entry("Current driver: {0}", "現在の駆動対象：{0}", "현재 구동 대상: {0}") },
                { "当前没有可用的驱动对象。", new Entry("No driver available.", "利用可能な駆動対象がありません。", "사용 가능한 구동 대상이 없습니다.") },
                { "整个模型（Avatar 根节点）", new Entry("Whole Avatar (avatar root)", "モデル全体（アバターのルート）", "전체 모델(아바타 루트)") },
                { "动骨根节点（直接驱动，可能不自然）", new Entry("PhysBone Root (direct drive, may look unnatural)", "動骨ルート（直接駆動、不自然になる場合あり）", "피직본 루트(직접 구동, 부자연스러울 수 있음)") },
                { "父级骨骼", new Entry("Parent Bone", "親ボーン", "부모 본") },
                { "自定义 Transform", new Entry("Custom Transform", "カスタム Transform", "사용자 지정 Transform") },

                // Build blocking and language.
                { "构建阻止", new Entry("Build Blocking", "ビルド阻止", "빌드 차단") },
                { "勾选后阻止对应工具在 Play Mode 处理 Avatar；设置立即生效并保持，取消勾选即恢复。", new Entry("Prevent the selected tool from processing the avatar in Play Mode. Changes apply immediately and persist; uncheck to restore.", "チェックすると対応ツールが Play Mode でアバターを処理するのを防ぎます。設定は即時反映され保持されます。解除すると元に戻ります。", "선택한 도구가 Play Mode에서 아바타를 처리하지 못하게 합니다. 설정은 즉시 적용되고 유지되며, 해제하면 복원됩니다.") },
                { "阻止 VRCFury 构建", new Entry("Block VRCFury Build", "VRCFury ビルドを阻止", "VRCFury 빌드 차단") },
                { "阻止 VRCFury 在 Play Mode 处理 Avatar。", new Entry("Prevent VRCFury from processing the avatar in Play Mode.", "VRCFury が Play Mode でアバターを処理するのを防ぎます。", "VRCFury가 Play Mode에서 아바타를 처리하지 못하게 합니다.") },
                { "未检测到 VRCFury。", new Entry("VRCFury not detected.", "VRCFury が見つかりません。", "VRCFury를 찾을 수 없습니다.") },
                { "阻止 MA / NDMF 构建", new Entry("Block MA / NDMF Build", "MA / NDMF ビルドを阻止", "MA / NDMF 빌드 차단") },
                { "阻止 Modular Avatar / NDMF 在 Play Mode 处理 Avatar。", new Entry("Prevent Modular Avatar / NDMF from processing the avatar in Play Mode.", "Modular Avatar / NDMF が Play Mode でアバターを処理するのを防ぎます。", "Modular Avatar / NDMF가 Play Mode에서 아바타를 처리하지 못하게 합니다.") },
                { "未检测到 NDMF / Modular Avatar。", new Entry("NDMF / Modular Avatar not detected.", "NDMF / Modular Avatar が見つかりません。", "NDMF / Modular Avatar를 찾을 수 없습니다.") },
                { "当前已在 Play Mode，修改将在下次进入 Play Mode 时生效。", new Entry("Currently in Play Mode; changes take effect the next time you enter Play Mode.", "現在 Play Mode 中です。変更は次回 Play Mode に入るときに反映されます。", "현재 Play Mode 중입니다. 변경 사항은 다음에 Play Mode에 들어갈 때 적용됩니다.") },
                { "语言 / Language", new Entry("Language", "言語", "언어") },
                { "界面语言", new Entry("Interface Language", "表示言語", "인터페이스 언어") },
                { "语言设置会保存在本机，切换后立即生效。", new Entry("The language is saved locally and applies immediately.", "言語設定はローカルに保存され、切り替え後すぐに反映されます。", "언어 설정은 로컬에 저장되며 전환 후 즉시 적용됩니다.") },
                { "没有可预览的动骨或驱动对象。", new Entry("No PhysBone or driver available for preview.", "プレビューできる動骨または駆動対象がありません。", "미리보기할 피직본 또는 구동 대상이 없습니다.") },
                { "找不到驱动对象。", new Entry("Driver not found.", "駆動対象が見つかりません。", "구동 대상을 찾을 수 없습니다.") },
                { "在 Play Mode 中找不到角色根节点。", new Entry("Avatar root not found in Play Mode.", "Play Mode でアバターのルートが見つかりません。", "Play Mode에서 아바타 루트를 찾을 수 없습니다.") },
                { "在 Play Mode 中找不到目标对象。", new Entry("Target object not found in Play Mode.", "Play Mode で対象オブジェクトが見つかりません。", "Play Mode에서 대상 오브젝트를 찾을 수 없습니다.") },
                { "目标对象上没有 VRCPhysBone。", new Entry("The target object has no VRCPhysBone.", "対象オブジェクトに VRCPhysBone がありません。", "대상 오브젝트에 VRCPhysBone이 없습니다.") },
                { "已暂存 Play Mode 参数，退出 Play Mode 后写回场景。", new Entry("Play Mode parameters staged; they will be written back after exiting Play Mode.", "Play Mode のパラメータを一時保存しました。終了後にシーンへ書き戻します。", "Play Mode 매개변수를 임시 저장했습니다. 종료 후 씬에 기록합니다.") },
                { "已写回 {0} 项 Play Mode 参数。", new Entry("Wrote back {0} Play Mode parameters.", "{0} 件の Play Mode パラメータを書き戻しました。", "Play Mode 매개변수 {0}개를 기록했습니다.") },
                { "找不到要写回参数的 PhysBone：{0}", new Entry("PhysBone not found for writing back parameters: {0}", "パラメータを書き戻す PhysBone が見つかりません：{0}", "매개변수를 기록할 PhysBone을 찾을 수 없습니다: {0}") },
                { "找不到原场景，已跳过写回：{0}", new Entry("Original scene not found; skipped write-back: {0}", "元のシーンが見つからないため、書き戻しをスキップしました：{0}", "원본 씬을 찾지 못해 기록을 건너뛰었습니다: {0}") },
                { "已重建 Play Mode 中新建的 PhysBone：{0}", new Entry("Recreated a PhysBone added in Play Mode: {0}", "Play Mode 中に追加された PhysBone を再作成しました：{0}", "Play Mode에서 추가한 PhysBone을 다시 생성했습니다: {0}") },
                { "找不到或无法重建要写回参数的 PhysBone：{0}", new Entry("PhysBone not found or could not be recreated for writing back parameters: {0}", "パラメータを書き戻す PhysBone が見つからないか、再作成できませんでした：{0}", "매개변수를 기록할 PhysBone을 찾지 못했거나 다시 생성할 수 없습니다: {0}") },
                { "未检测到 VRCPhysBone，无法写回参数。", new Entry("VRCPhysBone not detected; cannot write back parameters.", "VRCPhysBone が見つからないため、パラメータを書き戻せません。", "VRCPhysBone을 찾을 수 없어 매개변수를 기록할 수 없습니다.") },
                { "写回参数失败：{0}", new Entry("Failed to write back parameters: {0}", "パラメータの書き戻しに失敗しました：{0}", "매개변수 기록 실패: {0}") },
                { "修改 PhysBone 根骨骼", new Entry("Change PhysBone Root Bone", "PhysBone ルートボーンを変更", "PhysBone 루트 본 변경") },
                { "修改动骨设置", new Entry("Change PhysBone Settings", "動骨設定を変更", "피직본 설정 변경") },

                // PhysBone settings: section headers and labels.
                { "版本（Version）", new Entry("Version", "バージョン", "버전") },
                { "PhysBone 配置版本", new Entry("PhysBone configuration version", "PhysBone 設定バージョン", "PhysBone 구성 버전") },
                { "骨骼与变换 / Transforms", new Entry("Bones & Transforms", "ボーンと Transform", "본 및 Transform") },
                { "根骨骼（Root Transform）", new Entry("Root Transform", "ルート Transform", "루트 Transform") },
                { "动骨的根骨骼，默认是组件所在节点。", new Entry("Root bone of the PhysBone; defaults to the component's node.", "動骨のルートボーン。既定ではコンポーネントのノード。", "피직본의 루트 본. 기본값은 컴포넌트가 있는 노드입니다.") },
                { "忽略的骨骼（Ignore Transforms）", new Entry("Ignore Transforms", "無視する Transform", "무시할 Transform") },
                { "这些骨骼及其子级不会被 PhysBone 控制。", new Entry("These bones and their children are not controlled by the PhysBone.", "これらのボーンと子孫は PhysBone の制御対象外です。", "이 본과 하위 본은 PhysBone의 제어를 받지 않습니다.") },
                { "末端位置（Endpoint Position）", new Entry("Endpoint Position", "エンドポイント位置", "끝점 위치") },
                { "在链末端额外延伸的虚拟端点位置。", new Entry("Virtual endpoint position extending beyond the chain end.", "チェーン末端から追加で伸びる仮想エンドポイント位置。", "체인 끝에서 추가로 뻗는 가상 끝점 위치.") },
                { "多子骨骼模式（Multi Child Type）", new Entry("Multi Child Type", "複数子ボーン処理", "다중 자식 본 처리") },
                { "根骨骼有多个子级时使用哪一个。", new Entry("Which child to use when the root has multiple children.", "ルートに複数の子がある場合に使用する子。", "루트에 여러 자식이 있을 때 사용할 자식.") },
                { "受力 / Forces", new Entry("Forces", "力", "힘") },
                { "集成方式（Integration Type）", new Entry("Integration Type", "統合方式", "통합 방식") },
                { "简化：普通骨骼链；高级：允许更复杂的末端和子级处理。", new Entry("Simplified: normal bone chain; Advanced: more complex endpoint and child handling.", "簡易：通常のボーンチェーン。高度：より複雑なエンドポイントと子の処理。", "간단: 일반 본 체인, 고급: 더 복잡한 끝점 및 자식 처리.") },
                { "pull 回拉力（Pull）", new Entry("Pull", "引き戻し力（Pull）", "인력(Pull)") },
                { "越大，骨骼越快回到静止姿态。", new Entry("Higher values return the bone to rest faster.", "大きいほど早く静止姿勢へ戻ります。", "값이 클수록 본이 정지 자세로 빨리 돌아갑니다.") },
                { "spring 弹性（Spring）", new Entry("Spring", "弾性（Spring）", "스프링(Spring)") },
                { "越大，摆动越有弹性，回弹越明显。", new Entry("Higher values add more elasticity and bounce.", "大きいほど弾性と跳ね返りが強くなります。", "값이 클수록 탄성과 반동이 커집니다.") },
                { "stiffness 硬度（Stiffness）", new Entry("Stiffness", "硬さ（Stiffness）", "강성(Stiffness)") },
                { "越大，骨骼越硬，越不容易弯曲。", new Entry("Higher values make the bone stiffer and harder to bend.", "大きいほど硬く曲がりにくくなります。", "값이 클수록 더 단단해져 잘 휘지 않습니다.") },
                { "gravity 重力（Gravity）", new Entry("Gravity", "重力（Gravity）", "중력(Gravity)") },
                { "越大，骨骼越容易被向下拉。", new Entry("Higher values pull the bone downward more.", "大きいほど下方向に引かれやすくなります。", "값이 클수록 아래로 더 끌립니다.") },
                { "重力衰减（Gravity Falloff）", new Entry("Gravity Falloff", "重力減衰（Gravity Falloff）", "중력 감쇠(Gravity Falloff)") },
                { "沿骨骼链向下减弱重力的程度。", new Entry("How much gravity weakens along the chain.", "チェーンに沿って重力が弱まる度合い。", "체인을 따라 중력이 약해지는 정도.") },
                { "immobile 固定度（Immobile）", new Entry("Immobile", "固定度（Immobile）", "고정도(Immobile)") },
                { "越大，骨骼越不容易跟随整体移动。", new Entry("Higher values keep the bone from following whole-object movement.", "大きいほど全体移動に追従しにくくなります。", "값이 클수록 전체 이동을 덜 따라갑니다.") },
                { "固定方式（Immobile Type）", new Entry("Immobile Type", "固定方式（Immobile Type）", "고정 방식(Immobile Type)") },
                { "固定度作用在哪些运动上。", new Entry("Which motion the immobile value applies to.", "固定度を適用する運動。", "고정도가 적용되는 동작.") },
                { "碰撞 / Collision", new Entry("Collision", "衝突", "충돌") },
                { "允许碰撞（Allow Collision）", new Entry("Allow Collision", "衝突を許可", "충돌 허용") },
                { "是否允许动骨与碰撞体发生碰撞。", new Entry("Whether the PhysBone can collide with colliders.", "動骨がコライダーと衝突できるか。", "피직본이 콜라이더와 충돌할 수 있는지.") },
                { "碰撞权限（Collision Filter）", new Entry("Collision Filter", "衝突フィルター", "충돌 필터") },
                { "设置谁能触发碰撞，以及哪些内容类型参与碰撞。", new Entry("Who can trigger collisions and which content types participate.", "誰が衝突を起こせるか、どのコンテンツタイプが参加するか。", "누가 충돌을 일으킬 수 있고 어떤 콘텐츠 유형이 참여하는지.") },
                { "碰撞半径（Radius）", new Entry("Radius", "半径", "반경") },
                { "动骨碰撞体的粗细。", new Entry("Thickness of the PhysBone collider.", "動骨コライダーの太さ。", "피직본 콜라이더의 두께.") },
                { "碰撞体列表（Colliders）", new Entry("Colliders", "コライダー一覧", "콜라이더 목록") },
                { "参与碰撞的 VRCPhysBoneCollider 列表。", new Entry("VRCPhysBoneColliders participating in collision.", "衝突に参加する VRCPhysBoneCollider 一覧。", "충돌에 참여하는 VRCPhysBoneCollider 목록.") },
                { "拉伸与压缩 / Stretch & Squish", new Entry("Stretch & Squish", "伸縮", "늘림 및 압축") },
                { "最大拉伸（Max Stretch）", new Entry("Max Stretch", "最大伸び", "최대 늘림") },
                { "允许骨骼链被拉长的最大比例。", new Entry("Maximum stretch ratio of the chain.", "チェーンを伸ばせる最大比率。", "체인을 늘릴 수 있는 최대 비율.") },
                { "最大压缩（Max Squish）", new Entry("Max Squish", "最大圧縮", "최대 압축") },
                { "允许骨骼链被压缩的最大比例。", new Entry("Maximum squish ratio of the chain.", "チェーンを圧縮できる最大比率。", "체인을 압축할 수 있는 최대 비율.") },
                { "拉伸运动（Stretch Motion）", new Entry("Stretch Motion", "伸び運動", "늘림 동작") },
                { "拉伸时产生的额外运动强度。", new Entry("Extra motion applied while stretching.", "伸びたときに加わる追加運動。", "늘어날 때 추가되는 동작 강도.") },
                { "限位 / Limits", new Entry("Limits", "リミット", "제한") },
                { "限位方式（Limit Type）", new Entry("Limit Type", "制限方式", "제한 방식") },
                { "无：不限制；角度：限制 X/Z 摆动；铰链/极坐标：高级限位。", new Entry("None: no limits; Angle: limit X/Z swings; Hinge/Polar: advanced limits.", "なし：制限なし。角度：X/Z の揺れを制限。ヒンジ/極座標：高度な制限。", "없음: 제한 없음, 각도: X/Z 흔들림 제한, 힌지/극좌표: 고급 제한.") },
                { "最大 X 角度（Max Angle X）", new Entry("Max Angle X", "最大 X 角度", "최대 X 각도") },
                { "绕 X 轴允许的最大摆动角度。", new Entry("Maximum swing angle around X.", "X 軸回りの最大揺れ角度。", "X축 주변 최대 흔들림 각도.") },
                { "最大 Z 角度（Max Angle Z）", new Entry("Max Angle Z", "最大 Z 角度", "최대 Z 각도") },
                { "绕 Z 轴允许的最大摆动角度。", new Entry("Maximum swing angle around Z.", "Z 軸回りの最大揺れ角度。", "Z축 주변 최대 흔들림 각도.") },
                { "旋转限位（Limit Rotation）", new Entry("Limit Rotation", "回転リミット", "회전 제한") },
                { "旋转限位的中心角度。", new Entry("Center angle for rotation limits.", "回転リミットの中心角度。", "회전 제한의 중심 각도.") },
                { "旋转限位 X 曲线", new Entry("Limit Rotation X Curve", "回転リミット X カーブ", "회전 제한 X 커브") },
                { "绕 X 轴旋转限位的曲线。", new Entry("Rotation limit curve around X.", "X 軸回りの回転リミットカーブ。", "X축 회전 제한 커브.") },
                { "旋转限位 Y 曲线", new Entry("Limit Rotation Y Curve", "回転リミット Y カーブ", "회전 제한 Y 커브") },
                { "绕 Y 轴旋转限位的曲线。", new Entry("Rotation limit curve around Y.", "Y 軸回りの回転リミットカーブ。", "Y축 회전 제한 커브.") },
                { "旋转限位 Z 曲线", new Entry("Limit Rotation Z Curve", "回転リミット Z カーブ", "회전 제한 Z 커브") },
                { "绕 Z 轴旋转限位的曲线。", new Entry("Rotation limit curve around Z.", "Z 軸回りの回転リミットカーブ。", "Z축 회전 제한 커브.") },
                { "静止冻结轴（Static Freeze Axis）", new Entry("Static Freeze Axis", "静止フリーズ軸", "정지 고정 축") },
                { "冻结指定轴向的静止运动。", new Entry("Freeze static motion on the selected axis.", "指定軸の静止運動を固定。", "지정 축의 정지 동작을 고정.") },
                { "抓取与姿势 / Grab & Pose", new Entry("Grab & Pose", "グラブとポーズ", "잡기 및 포즈") },
                { "允许抓取（Allow Grabbing）", new Entry("Allow Grabbing", "グラブを許可", "잡기 허용") },
                { "是否允许玩家抓取这条动骨。", new Entry("Whether players can grab this PhysBone.", "プレイヤーがこの動骨を掴めるか。", "플레이어가 이 피직본을 잡을 수 있는지.") },
                { "抓取权限（Grab Filter）", new Entry("Grab Filter", "グラブフィルター", "잡기 필터") },
                { "设置谁能抓取，以及哪些内容类型可以抓取。", new Entry("Who can grab and which content types can be grabbed.", "誰が掴めるか、どのコンテンツタイプを掴めるか。", "누가 잡을 수 있고 어떤 콘텐츠 유형을 잡을 수 있는지.") },
                { "允许摆姿势（Allow Posing）", new Entry("Allow Posing", "ポーズを許可", "포즈 허용") },
                { "是否允许玩家在菜单里摆弄这条动骨。", new Entry("Whether players can pose this PhysBone from the menu.", "メニューからこの動骨をポーズできるか。", "메뉴에서 이 피직본을 포즈할 수 있는지.") },
                { "摆姿势权限（Pose Filter）", new Entry("Pose Filter", "ポーズフィルター", "포즈 필터") },
                { "设置谁能摆姿势，以及哪些内容类型可以摆姿势。", new Entry("Who can pose and which content types can be posed.", "誰がポーズできるか、どのコンテンツタイプをポーズできるか。", "누가 포즈할 수 있고 어떤 콘텐츠 유형을 포즈할 수 있는지.") },
                { "吸附到手（Snap To Hand）", new Entry("Snap To Hand", "手にスナップ", "손에 스냅") },
                { "抓取时是否直接吸附到手的位置。", new Entry("Whether grabbing snaps directly to the hand.", "掴んだとき手の位置に直接スナップするか。", "잡을 때 손 위치로 바로 스냅할지.") },
                { "抓取移动（Grab Movement）", new Entry("Grab Movement", "グラブ移動", "잡기 이동") },
                { "抓取时骨骼跟随手部移动的比例。", new Entry("How much the bone follows the hand while grabbed.", "掴んでいる間、手の動きに追従する割合。", "잡는 동안 본이 손을 따라가는 비율.") },
                { "选项 / Options", new Entry("Options", "オプション", "옵션") },
                { "根骨骼被动画驱动（Is Animated）", new Entry("Is Animated", "アニメーション駆動", "애니메이션 구동") },
                { "动画会驱动根骨骼时勾选；直接驱动根节点预览时插件会临时开启。", new Entry("Enable when animation drives the root bone; the plugin temporarily enables it when previewing the root directly.", "アニメーションがルートボーンを駆動する場合に有効。ルート直接プレビュー時は一時的に有効化されます。", "애니메이션이 루트 본을 구동할 때 선택하세요. 루트 직접 미리보기 시 임시로 켜집니다.") },
                { "禁用时重置（Reset When Disabled）", new Entry("Reset When Disabled", "無効時にリセット", "비활성화 시 초기화") },
                { "组件禁用时是否让动骨回到初始姿态。", new Entry("Whether the PhysBone returns to its initial pose when disabled.", "無効時に動骨を初期姿勢へ戻すか。", "비활성화 시 피직본을 초기 자세로 되돌릴지.") },
                { "控制参数名（Parameter）", new Entry("Parameter", "制御パラメータ名", "제어 매개변수 이름") },
                { "读取 Angle / Stretch / Squish 等参数时使用的名字。", new Entry("Name used when reading Angle / Stretch / Squish parameters.", "Angle / Stretch / Squish などのパラメータ読み取りに使う名前。", "Angle / Stretch / Squish 등의 매개변수를 읽을 때 사용하는 이름.") },
                { "显示 Gizmo（Show Gizmos）", new Entry("Show Gizmos", "Gizmo を表示", "Gizmo 표시") },
                { "在 Scene 视图显示动骨辅助线。", new Entry("Show PhysBone guides in the Scene view.", "Scene ビューに動骨ガイドを表示。", "Scene 뷰에 피직본 가이드 표시.") },
                { "骨骼透明度（Bone Opacity）", new Entry("Bone Opacity", "ボーン透明度", "본 투명도") },
                { "Gizmo 中骨骼辅助线的透明度。", new Entry("Opacity of bone guides in the Gizmo.", "Gizmo のボーンガイド透明度。", "Gizmo 본 가이드 투명도.") },
                { "限位透明度（Limit Opacity）", new Entry("Limit Opacity", "リミット透明度", "제한 투명도") },
                { "Gizmo 中限位范围的透明度。", new Entry("Opacity of limit ranges in the Gizmo.", "Gizmo のリミット範囲透明度。", "Gizmo 제한 범위 투명도.") },
                { "忽略其他 PhysBone（Ignore Other PhysBones）", new Entry("Ignore Other PhysBones", "他の PhysBone を無視", "다른 PhysBone 무시") },
                { "是否忽略其他 PhysBone 对本链条的影响。", new Entry("Whether other PhysBones affect this chain.", "他の PhysBone がこのチェーンに与える影響を無視するか。", "다른 PhysBone이 이 체인에 미치는 영향을 무시할지.") },
                { "曲线（Curve）", new Entry("Curve", "カーブ", "커브") },
                { "数值沿骨骼链的衰减曲线。", new Entry("Falloff curve along the chain.", "チェーンに沿った減衰カーブ。", "체인을 따른 감쇠 커브.") },
                { "允许自己（Allow Self）", new Entry("Allow Self", "自分を許可", "자신 허용") },
                { "允许他人（Allow Others）", new Entry("Allow Others", "他人を許可", "다른 사람 허용") },
                { "内容类型（Content Types）", new Entry("Content Types", "コンテンツタイプ", "콘텐츠 유형") },

                // Enum values.
                { "关闭", new Entry("Off", "オフ", "끄기") },
                { "开启", new Entry("On", "オン", "켜기") },
                { "其他（高级）", new Entry("Other (Advanced)", "その他（詳細）", "기타(고급)") },
                { "无", new Entry("None", "なし", "없음") },
                { "角度", new Entry("Angle", "角度", "각도") },
                { "铰链", new Entry("Hinge", "ヒンジ", "힌지") },
                { "极坐标", new Entry("Polar", "極座標", "극좌표") },
                { "所有运动", new Entry("All Motion", "すべての運動", "모든 동작") },
                { "世界坐标", new Entry("World", "ワールド座標", "월드 좌표") },
                { "忽略", new Entry("Ignore", "無視", "무시") },
                { "第一个", new Entry("First", "最初", "첫 번째") },
                { "平均", new Entry("Average", "平均", "평균") },
                { "简化", new Entry("Simplified", "簡易", "간단") },
                { "高级", new Entry("Advanced", "高度", "고급") },

                // Compact force strip.
                { "pull 回拉力", new Entry("Pull", "引き戻し力", "인력") },
                { "回正速度：越大，骨骼越快回到静止姿态。", new Entry("Return speed: higher values return to rest faster.", "戻り速度：大きいほど早く静止姿勢へ戻ります。", "복귀 속도: 값이 클수록 정지 자세로 빨리 돌아갑니다.") },
                { "spring 弹性", new Entry("Spring", "弾性", "스프링") },
                { "回弹强度：越大，摆动越有弹性，回弹越明显。", new Entry("Bounce strength: higher values add elasticity and rebound.", "跳ね返り強度：大きいほど弾性と反動が強くなります。", "반동 강도: 값이 클수록 탄성과 반동이 커집니다.") },
                { "stiffness 硬度", new Entry("Stiffness", "硬さ", "강성") },
                { "抗弯曲：越大，骨骼越硬，越不容易弯曲。", new Entry("Bend resistance: higher values make bones stiffer.", "曲げ抵抗：大きいほど硬く曲がりにくくなります。", "굽힘 저항: 값이 클수록 더 단단해집니다.") },
                { "gravity 重力", new Entry("Gravity", "重力", "중력") },
                { "下垂力：越大，骨骼越容易被向下拉。", new Entry("Downward pull: higher values pull bones down more.", "下向きの力：大きいほど下に引かれやすくなります。", "아래로 당기는 힘: 값이 클수록 더 아래로 끌립니다.") },
                { "immobile 固定度", new Entry("Immobile", "固定度", "고정도") },
                { "抗整体移动：越大，骨骼越不容易跟随整体移动。", new Entry("Whole-motion resistance: higher values reduce following whole-object movement.", "全体移動への抵抗：大きいほど全体移動に追従しにくくなります。", "전체 이동 저항: 값이 클수록 전체 이동을 덜 따라갑니다.") }
            };
    }
}
