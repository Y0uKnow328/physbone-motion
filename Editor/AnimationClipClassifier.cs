using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarChamber.PhysBoneMotion
{
    /// <summary>
    /// Categories used by the detailed animation scan.
    /// Enum order is the order shown in the UI.
    /// </summary>
    public enum AnimationClipCategory
    {
        Locomotion = 0,
        CrouchWalk = 1,
        Crawl = 2,
        Jump = 3,
        Fly = 4,
        Sit = 5,
        Idle = 6,
        Emote = 7,
        Gesture = 8,
        Other = 9
    }

    public sealed class AnimationClipGroup
    {
        public AnimationClipCategory Category;
        public string Name;
        public readonly List<AnimationClip> Clips = new List<AnimationClip>();
    }

    /// <summary>
    /// Name-based classifier for animation clips. It is deliberately heuristic so it
    /// works with VRChat proxy animations, GoGo Loco, Gesture Manager and common
    /// user-named clips without depending on any specific plugin.
    /// </summary>
    public static class AnimationClipClassifier
    {
        public static string GetDisplayName(AnimationClipCategory category)
        {
            switch (category)
            {
                case AnimationClipCategory.Locomotion: return "行走 / 移动";
                case AnimationClipCategory.CrouchWalk: return "蹲走";
                case AnimationClipCategory.Crawl: return "爬行";
                case AnimationClipCategory.Jump: return "跳跃";
                case AnimationClipCategory.Fly: return "飞行 / 悬浮";
                case AnimationClipCategory.Sit: return "坐姿 / 躺卧";
                case AnimationClipCategory.Idle: return "待机";
                case AnimationClipCategory.Emote: return "表情 / 舞蹈";
                case AnimationClipCategory.Gesture: return "手势";
                default: return "其他 / 系统";
            }
        }

        public static bool IsLocomotionCategory(AnimationClipCategory category)
        {
            return category == AnimationClipCategory.Locomotion
                   || category == AnimationClipCategory.CrouchWalk
                   || category == AnimationClipCategory.Crawl;
        }

        private static readonly Dictionary<AnimationClip, bool> ScaleCurveCache =
            new Dictionary<AnimationClip, bool>();

        /// <summary>
        /// True for clips that can be shown continuously: looping clips and clips
        /// with real motion. Zero-length single-frame poses are not useful in a loop.
        /// </summary>
        public static bool IsLoopCapable(AnimationClip clip)
        {
            return clip != null && (clip.isLooping || clip.length > 0.01f);
        }

        /// <summary>
        /// Scale curves are the main source of stretched, deformed meshes during
        /// clip preview. Cached because the playlist UI queries it per row.
        /// </summary>
        public static bool HasScaleCurves(AnimationClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            bool cached;
            if (ScaleCurveCache.TryGetValue(clip, out cached))
            {
                return cached;
            }

            var result = false;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal))
                {
                    result = true;
                    break;
                }
            }

            ScaleCurveCache[clip] = result;
            return result;
        }

        /// <summary>
        /// Rejects clips that are known to be unsafe when played on their own.
        /// Very short non-looping clips are usually controller transitions or action
        /// poses (for example GoGo Loco go_dash_backward) and can twist the avatar
        /// into a deformed pose even though all bone scales stay at 1.
        /// </summary>
        public static bool IsSafeStandalone(AnimationClip clip, out string reason)
        {
            if (clip == null)
            {
                reason = "空动画";
                return false;
            }

            if (!clip.isHumanMotion)
            {
                reason = "非 Humanoid 动画可能拉伸骨骼";
                return false;
            }

            if (HasScaleCurves(clip))
            {
                reason = "动画包含缩放曲线，可能拉伸骨骼";
                return false;
            }

            if (!clip.isLooping && clip.length < 0.1f)
            {
                reason = "过短的过渡/动作片段，单独播放可能畸形（建议用角色控制器播放）";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool CanUseInPlaylist(AnimationClip clip, out string reason)
        {
            if (!IsSafeStandalone(clip, out reason))
            {
                return false;
            }

            if (!IsLoopCapable(clip))
            {
                reason = "该动画不能循环播放";
                return false;
            }

            return true;
        }

        public static string GetClipTooltip(AnimationClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            var length = clip.length > 0.01f
                ? clip.length.ToString("0.00") + " 秒"
                : "0 秒（静止姿势）";
            var type = clip.isHumanMotion ? "Humanoid" : "非 Humanoid";
            var loop = clip.isLooping ? "是" : (clip.length > 0.01f ? "否（播放时循环）" : "否");
            var tooltip = clip.name
                          + "\n长度：" + length
                          + "\n类型：" + type
                          + "\n循环：" + loop;

            string safety;
            if (!IsSafeStandalone(clip, out safety))
            {
                tooltip += "\n受保护：" + safety;
            }

            return tooltip;
        }

        public static AnimationClipCategory Classify(AnimationClip clip)
        {
            if (clip == null)
            {
                return AnimationClipCategory.Other;
            }

            var name = clip.name.ToLowerInvariant();

            // Calibration, smoothing and generated utility clips.
            if (IsUtilityClip(name))
            {
                return AnimationClipCategory.Other;
            }

            if (Has(name, "fly", "flying", "flight", "hover", "glide", "swim",
                    "飞行", "悬浮", "游泳"))
            {
                return AnimationClipCategory.Fly;
            }

            if (Has(name, "jump", "leap", "vault", "fall", "land", "跳跃", "跳起"))
            {
                return AnimationClipCategory.Jump;
            }

            if (Has(name, "sleep", "laydown", "lay down", "lying", "supine",
                    "sit", "sitting", "seated", "seat", "chair",
                    "cross leg", "cross_leg", "kneel", "knees", "seiza",
                    "坐", "躺", "卧", "跪"))
            {
                return AnimationClipCategory.Sit;
            }

            if (Has(name, "crawl", "low crawl", "low_crawl", "all 4", "all_4", "all4",
                    "prone", "匍匐", "爬行"))
            {
                return AnimationClipCategory.Crawl;
            }

            if (Has(name, "gesture", "fist", "open", "gun", "rock", "thumbs",
                    "victory", "finger", "peace", "hand", "asl", "sign",
                    "手势", "拳", "指"))
            {
                return AnimationClipCategory.Gesture;
            }

            if (Has(name, "dance", "dancing", "club", "groove",
                    "emote", "laugh", "wave", "clap", "point", "cheer", "drum",
                    "die", "sad", "angry", "disbelief", "disapprove",
                    "face", "mood", "expression", "backflip", "smile", "eye",
                    "舞", "表情", "笑", "挥手"))
            {
                return AnimationClipCategory.Emote;
            }

            if (Has(name, "crouch", "sneak", "duck", "squat", "蹲"))
            {
                return Has(name, "walk", "strafe", "move", "run", "jog", "shuffle",
                           "forward", "back", "left", "right", "走", "行")
                    ? AnimationClipCategory.CrouchWalk
                    : AnimationClipCategory.Idle;
            }

            if (Has(name, "idle", "afk", "still", "stand", "wait",
                    "tpose", "t pose", "ikpose", "ik pose", "init",
                    "待机", "静止", "站立"))
            {
                return AnimationClipCategory.Idle;
            }

            if (Has(name, "walk", "walking", "run", "running", "sprint", "strafe",
                    "shuffle", "jog", "locomotion", "dash", "grind", "move",
                    "turn", "rotate", "行走", "走路", "步行", "跑", "移动", "转身"))
            {
                return AnimationClipCategory.Locomotion;
            }

            return AnimationClipCategory.Other;
        }

        public static List<AnimationClip> CollectLocomotionClips(IEnumerable<AnimationClip> clips)
        {
            var result = new List<AnimationClip>();
            if (clips == null)
            {
                return result;
            }

            var seen = new HashSet<AnimationClip>();
            foreach (var clip in clips)
            {
                if (clip == null || !seen.Add(clip))
                {
                    continue;
                }

                if (IsLocomotionCategory(Classify(clip)))
                {
                    result.Add(clip);
                }
            }

            result.Sort(CompareLocomotion);
            return result;
        }

        /// <summary>
        /// Strict default filter for the "读取动作" section: walk, run, sprint,
        /// strafe, shuffle, crouch-walk and crawl. Dash / grind / turn clips stay
        /// available in the detailed "行走 / 移动" category but are not shown by default.
        /// </summary>
        public static bool IsDefaultLocomotionClip(AnimationClip clip)
        {
            if (clip == null || !clip.isHumanMotion || !IsLoopCapable(clip))
            {
                return false;
            }

            var category = Classify(clip);
            if (category == AnimationClipCategory.CrouchWalk
                || category == AnimationClipCategory.Crawl)
            {
                return true;
            }

            if (category != AnimationClipCategory.Locomotion)
            {
                return false;
            }

            var name = clip.name.ToLowerInvariant();
            return Has(name, "walk", "walking", "run", "running", "sprint", "strafe",
                       "shuffle", "jog", "locomotion",
                       "行走", "走路", "步行", "跑", "移动");
        }

        public static List<AnimationClip> CollectDefaultLocomotionClips(IEnumerable<AnimationClip> clips)
        {
            var result = new List<AnimationClip>();
            if (clips == null)
            {
                return result;
            }

            var seen = new HashSet<AnimationClip>();
            foreach (var clip in clips)
            {
                if (clip == null || !seen.Add(clip))
                {
                    continue;
                }

                if (IsDefaultLocomotionClip(clip))
                {
                    string reason;
                    if (IsSafeStandalone(clip, out reason))
                    {
                        result.Add(clip);
                    }
                }
            }

            result.Sort(CompareLocomotion);
            return result;
        }

        public static List<AnimationClipGroup> Categorize(IEnumerable<AnimationClip> clips)
        {
            var groups = new List<AnimationClipGroup>();
            if (clips == null)
            {
                return groups;
            }

            var byCategory = new Dictionary<AnimationClipCategory, AnimationClipGroup>();
            var seen = new HashSet<AnimationClip>();
            foreach (var clip in clips)
            {
                if (clip == null || !seen.Add(clip))
                {
                    continue;
                }

                var category = Classify(clip);
                AnimationClipGroup group;
                if (!byCategory.TryGetValue(category, out group))
                {
                    group = new AnimationClipGroup
                    {
                        Category = category,
                        Name = GetDisplayName(category)
                    };
                    byCategory[category] = group;
                }

                group.Clips.Add(clip);
            }

            foreach (AnimationClipCategory category in Enum.GetValues(typeof(AnimationClipCategory)))
            {
                AnimationClipGroup group;
                if (!byCategory.TryGetValue(category, out group))
                {
                    continue;
                }

                group.Clips.Sort(CompareGroupClips);
                groups.Add(group);
            }

            return groups;
        }

        private static int CompareLocomotion(AnimationClip a, AnimationClip b)
        {
            var rankA = LocomotionRank(Classify(a));
            var rankB = LocomotionRank(Classify(b));
            if (rankA != rankB)
            {
                return rankA.CompareTo(rankB);
            }

            return CompareGroupClips(a, b);
        }

        private static int LocomotionRank(AnimationClipCategory category)
        {
            switch (category)
            {
                case AnimationClipCategory.Locomotion: return 0;
                case AnimationClipCategory.CrouchWalk: return 1;
                default: return 2;
            }
        }

        private static int CompareGroupClips(AnimationClip a, AnimationClip b)
        {
            if (a == null)
            {
                return b == null ? 0 : 1;
            }

            if (b == null)
            {
                return -1;
            }

            var moving = (b.length > 0.05f).CompareTo(a.length > 0.05f);
            if (moving != 0)
            {
                return moving;
            }

            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUtilityClip(string name)
        {
            return name.StartsWith("go_root", StringComparison.Ordinal)
                   || name.StartsWith("go_fbt", StringComparison.Ordinal)
                   || name.StartsWith("go_additive", StringComparison.Ordinal)
                   || name.StartsWith("go_empty", StringComparison.Ordinal)
                   || name.StartsWith("go_smooth", StringComparison.Ordinal)
                   || name.StartsWith("go_floatsmooth", StringComparison.Ordinal)
                   || name.StartsWith("go_poseradial", StringComparison.Ordinal)
                   || name.StartsWith("go_pose_", StringComparison.Ordinal)
                   || name.Contains("empty")
                   || name.Contains("turn off")
                   || name.Contains("chestbutton")
                   || name.Contains("puff-particle");
        }

        private static bool Has(string name, params string[] keywords)
        {
            for (var i = 0; i < keywords.Length; i++)
            {
                if (name.IndexOf(keywords[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
