using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using Extensions = TaleWorlds.Library.Extensions;
using Helpers;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace JoinMyKingdom
{
    internal class RecruitExiledClans : CampaignBehaviorBase
    {
        private bool autojoinon { get; set; }
        public RecruitExiledClans(AIClansAutoJoin setting)
        {
            autojoinon = setting.autojoinon;
        }
        public override void SyncData(IDataStore dataStore)
        { }
        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, new Action(this.AIClansAutoJoin));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.JoinChat));
        }
        private void JoinChat(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("Dialog_Join1", "hero_main_options", "Dialog_Join1", "{=Dialog_Join1}Join my Kingdom,It's the only way for your clan to survive.", new ConversationSentence.OnConditionDelegate(this.CanJoinMyKingdom), null, 100, null, null);
            starter.AddDialogLine("Dialog_Join2", "Dialog_Join1", "close_window", "{=Dialog_Join2}Aye. We will join you.", null, new ConversationSentence.OnConsequenceDelegate(this.JoinMyKingdomDialogConsequence), 100, null);
        }
        private bool CanJoinMyKingdom()
        {
            // 检查对话对象是否为英雄
            if (!CharacterObject.OneToOneConversationCharacter.IsHero)
                return false;

            Hero targetHero = Hero.OneToOneConversationHero;
            Clan targetClan = targetHero.Clan;
            Clan playerClan = Hero.MainHero.Clan;

            // 基础条件检查
            if (targetClan == null || playerClan?.Kingdom == null)
                return false;

            // 目标家族条件
            bool hasNoKingdom = targetClan.Kingdom == null;
            bool isClanLeader = targetHero.IsClanLeader;
            bool isNotMinorFaction = !targetClan.IsMinorFaction;
            bool isNotRebelOrMercenary = !targetClan.IsRebelClan && !targetClan.IsClanTypeMercenary;
            bool isAliveAndActive = targetHero.IsAlive && targetHero.IsActive;
            bool isNotEliminated = !targetClan.IsEliminated;

            // 综合条件判断
            return hasNoKingdom &&
                   isClanLeader &&
                   isNotMinorFaction &&
                   isNotRebelOrMercenary &&
                   isAliveAndActive &&
                   isNotEliminated;
        }
        private void AIClansAutoJoin()
        {
            if (!autojoinon)
                return;
            try
            {
                // 获取所有符合条件的AI家族
                var eligibleClans = Clan.All.Where(clan =>
                    clan != Hero.MainHero.Clan &&                    // 排除玩家家族
                    clan.IsEliminated == false &&                   // 家族未被消灭
                    clan.Kingdom == null &&                         // 当前没有国家
                    !clan.IsMinorFaction &&                         // 不是次要家族
                    !clan.IsRebelClan &&                           // 不是叛军家族
                    !clan.IsClanTypeMercenary &&                    // 不是雇佣兵家族
                    clan.Leader != null &&                          // 有领袖
                    clan.Leader.IsAlive &&                          // 领袖存活
                    clan.Leader.IsActive                            // 领袖活跃
                ).ToList();

                if (eligibleClans.Count == 0)
                {
                    Debug.Print("[自动加入] 没有符合条件的流亡家族");
                    return;
                }

                Debug.Print($"[自动加入] 找到 {eligibleClans.Count} 个符合条件的流亡家族");

                foreach (var clan in eligibleClans)
                {
                    ProcessClanKingdomDecision(clan);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理单个家族的王国加入决策
        /// </summary>
        private void ProcessClanKingdomDecision(Clan clan)
        {
            try
            {
                // 1. 尝试加入最好朋友的国家
                Kingdom bestFriendKingdom = GetBestFriendKingdom(clan.Leader);

                if (bestFriendKingdom != null && CanJoinKingdom(clan, bestFriendKingdom))
                {
                    JoinKingdom(clan, bestFriendKingdom, null);
                    return;
                }

                // 2. 如果没有好朋友，尝试加入关系最好的国家
                Kingdom bestRelationKingdom = GetBestRelationKingdom(clan.Leader);

                if (bestRelationKingdom != null && CanJoinKingdom(clan, bestRelationKingdom))
                {
                    JoinKingdom(clan, bestRelationKingdom, null);
                    return;
                }

                // 3. 尝试加入最合适的国家（基于地理位置、实力等）
                //Kingdom mostSuitableKingdom = GetMostSuitableKingdom(clan);

                //if (mostSuitableKingdom != null && CanJoinKingdom(clan, mostSuitableKingdom))
                //{
                //    JoinKingdom(clan, mostSuitableKingdom, "最合适");
                //    return;
                //}

                Debug.Print($"[自动加入] {clan.Name} 没有找到合适的国家加入");
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 处理家族 {clan.Name} 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取最好朋友所在的国家
        /// </summary>
        private Kingdom GetBestFriendKingdom(Hero clanLeader)
        {
            try
            {
                // 获取所有朋友（关系大于0的）
                var friends = Hero.AllAliveHeroes
                    .Where(hero =>
                        hero != clanLeader &&
                        hero.Clan != null &&
                        hero.Clan.Kingdom != null &&
                        hero.GetRelation(clanLeader) >= 0) // 关系阈值
                    .OrderByDescending(hero => hero.GetRelation(clanLeader))
                    .ToList();

                if (friends.Count == 0)
                {
                    return null;
                }

                // 优先选择关系最好的朋友
                var bestFriend = friends.First();

                // 如果没有重要朋友，选择关系最好的普通朋友
                return bestFriend.Clan.Kingdom;
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 获取最好朋友国家时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取关系最好的国家
        /// </summary>
        private Kingdom GetBestRelationKingdom(Hero clanLeader)
        {
            try
            {
                var kingdoms = Kingdom.All
                    .Where(kingdom =>
                        kingdom != null &&
                        kingdom.RulingClan != null &&
                        kingdom.RulingClan != clanLeader.Clan &&
                        kingdom.IsEliminated == false)
                    .ToList();

                if (kingdoms.Count == 0)
                {
                    return null;
                }

                // 计算与每个国家的关系分数
                var kingdomScores = new Dictionary<Kingdom, float>();

                foreach (var kingdom in kingdoms)
                {
                    float relationScore = CalculateKingdomRelationScore(clanLeader, kingdom);
                    kingdomScores[kingdom] = relationScore;
                }

                // 选择关系最好的国家
                var bestKingdom = kingdomScores
                    .OrderByDescending(kv => kv.Value)
                    .FirstOrDefault();

                // 只考虑关系分数高于阈值的国家
                if (bestKingdom.Value >= 20f) // 关系阈值
                {
                    return bestKingdom.Key;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 获取关系最好国家时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取最合适的国家（基于综合因素）
        /// </summary>
        private Kingdom GetMostSuitableKingdom(Clan clan)
        {
            try
            {
                var kingdoms = Kingdom.All
                    .Where(kingdom =>
                        kingdom != null &&
                        kingdom.RulingClan != null &&
                        kingdom.RulingClan != clan &&
                        kingdom.IsEliminated == false)
                    .ToList();

                if (kingdoms.Count == 0)
                {
                    return null;
                }

                // 计算每个国家的适合度分数
                var kingdomScores = new Dictionary<Kingdom, float>();

                foreach (var kingdom in kingdoms)
                {
                    float suitabilityScore = CalculateKingdomSuitabilityScore(clan, kingdom);
                    kingdomScores[kingdom] = suitabilityScore;
                }

                // 选择最适合的国家
                var bestKingdom = kingdomScores
                    .OrderByDescending(kv => kv.Value)
                    .FirstOrDefault();

                // 只考虑适合度分数高于阈值的国家
                if (bestKingdom.Value >= 0.5f) // 适合度阈值
                {
                    return bestKingdom.Key;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 获取最合适国家时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算国家关系分数
        /// </summary>
        private float CalculateKingdomRelationScore(Hero clanLeader, Kingdom kingdom)
        {
            float score = 0f;

            try
            {
                // 1. 与国家统治者的关系
                if (kingdom.RulingClan != null && kingdom.RulingClan.Leader != null)
                {
                    score += clanLeader.GetRelation(kingdom.RulingClan.Leader) * 0.5f;
                }

                // 2. 与国家其他重要贵族的关系
                var importantNobles = kingdom.Clans
                    .Where(clan => clan != kingdom.RulingClan && clan.Leader != null)
                    .Take(5) // 只考虑前5个重要贵族
                    .ToList();

                foreach (var nobleClan in importantNobles)
                {
                    score += clanLeader.GetRelation(nobleClan.Leader) * 0.1f;
                }

                return score;
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 计算国家关系分数时出错: {ex.Message}");
                return score;
            }
        }

        /// <summary>
        /// 计算国家适合度分数
        /// </summary>
        private float CalculateKingdomSuitabilityScore(Clan clan, Kingdom kingdom)
        {
            float score = 0f;

            try
            {
                // 3. 文化兼容性
                float cultureScore = CalculateCultureScore(clan, kingdom);
                score += cultureScore * 0.2f;
                return score;
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 计算国家适合度分数时出错: {ex.Message}");
                return score;
            }
        }

        /// <summary>
        /// 检查是否可以加入国家
        /// </summary>
        private bool CanJoinKingdom(Clan clan, Kingdom kingdom)
        {
            try
            {
                // 基本检查
                if (clan == null || kingdom == null)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] 检查是否可以加入国家时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行加入国家操作
        /// </summary>
        private void JoinKingdom(Clan clan, Kingdom kingdom, string reason)
        {
            try
            {
                // 记录加入前的状态
                Debug.Print($"[自动加入] {clan.Name} 正在加入 {kingdom.Name} ({reason})");

                // 执行加入操作
                ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, default(CampaignTime), true);

                // 提高与统治者的关系
                if (kingdom.RulingClan != null && kingdom.RulingClan.Leader != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        clan.Leader,
                        kingdom.RulingClan.Leader,
                        20, // 关系提升值
                        true
                    );
                }

                // 记录日志
                Debug.Print($"[自动加入] {clan.Name} 成功加入 {kingdom.Name} ({reason})");

                Kingdom playerKingdom = Hero.MainHero?.Clan?.Kingdom;

                // 显示游戏内消息
                if (playerKingdom != null)
                {
                    if (kingdom == playerKingdom)
                    {
                        // 加入玩家王国 - 蓝色
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{clan.Name} has joined {kingdom.Name}",
                            new Color(0.3f, 0.7f, 1f) // 蓝色
                        ));
                    }
                    else if (kingdom.IsAtWarWith(playerKingdom))
                    {
                        // 加入敌对国家 - 红色
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{clan.Name} has joined {kingdom.Name}",
                            new Color(1f, 0.3f, 0.3f) // 红色
                        ));
                    }
                    else
                    {
                        // 加入其他国家 - 白色
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{clan.Name} has joined {kingdom.Name}",
                            Color.White
                        ));
                    }
                }
                else
                {
                    // 玩家没有王国 - 白色
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{clan.Name} has joined {kingdom.Name}",
                        Color.White
                    ));
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[自动加入] {clan.Name} 加入 {kingdom.Name} 失败: {ex.Message}");
            }
        }
        private float CalculateCultureScore(Clan clan, Kingdom kingdom)
        {
            // 相同文化获得更高分数
            return clan.Culture == kingdom.Culture ? 1f : 0.3f;
        }

        private void JoinMyKingdomDialogConsequence()
        {
            Clan mainheroclan = Hero.MainHero.Clan;
            Kingdom tempkingdom;
            tempkingdom = ((mainheroclan != null) ? mainheroclan.Kingdom : null);
            Kingdom playerKingdom = tempkingdom;
            Clan clan = Hero.OneToOneConversationHero.Clan;
            ChangeKingdomAction.ApplyByJoinToKingdom(clan, playerKingdom, default(CampaignTime), true);
            ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, 30, true, true);
        }
    }
}
