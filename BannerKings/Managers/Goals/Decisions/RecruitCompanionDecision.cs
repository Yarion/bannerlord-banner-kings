﻿using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace BannerKings.Managers.Goals.Decisions
{
    internal class RecruitCompanionDecision : Goal
    {
        private readonly List<CompanionType> companionTypes;
        private CompanionType selectedCompanionType;

        public RecruitCompanionDecision() : base("goal_recruit_companion_decision", GoalUpdateType.Manual)
        {
            var name = new TextObject("{=!}Recruit Companion");
            var description = new TextObject("{!=}Select a type of companion to recruit.");

            Initialize(name, description);

            companionTypes = new List<CompanionType>
            {
                new("commander", "Commander", "A companion that meets the criteria for a Commander.", 5000, 100,
                    new List<TraitObject>
                    {
                        DefaultTraits.Commander
                    },
                    new List<PerkObject>(),
                    new List<SkillObject>
                    {
                        DefaultSkills.Leadership, 
                        DefaultSkills.Tactics
                    }),
                new("thief", "Thief", "A companion that meets the criteria for a Thief.", 5000, 100,
                    new List<TraitObject>
                    {
                        DefaultTraits.Thief
                    },
                    new List<PerkObject>(),
                    new List<SkillObject>
                    {
                        DefaultSkills.Roguery
                    }),
                new("surgeon", "Surgeon", "A companion that meets the criteria for a Surgeon.", 5000, 100,
                    new List<TraitObject>
                    {
                        DefaultTraits.Surgery
                    },
                    new List<PerkObject>(),
                    new List<SkillObject>
                    {
                        DefaultSkills.Medicine
                    }),
                new("caravaneer", "Caravaneer", "A companion that meets the criteria for a Caravaneer.", 5000, 100,
                    new List<TraitObject>
                    {
                        DefaultTraits.Manager
                    },
                    new List<PerkObject>(),
                    new List<SkillObject> { DefaultSkills.Steward, DefaultSkills.Scouting }),
                new("warrior", "Warrior", "A companion that meets the criteria for a Warrior.", 5000, 100,
                    new List<TraitObject>
                    {
                        DefaultTraits.Fighter
                    },
                    new List<PerkObject>(),
                    new List<SkillObject>
                    { 
                        DefaultSkills.OneHanded, 
                        DefaultSkills.TwoHanded, 
                        DefaultSkills.Polearm,
                        DefaultSkills.Bow, 
                        DefaultSkills.Crossbow, 
                        DefaultSkills.Throwing, 
                        DefaultSkills.Riding,
                        DefaultSkills.Athletics
                    })
            };
        }

        internal override bool IsAvailable()
        {
            return true;
        }

        internal override bool IsFulfilled(out List<TextObject> failedReasons)
        {
            failedReasons = new List<TextObject>();

            var gold = GetFulfiller().Gold;
            var influence = GetFulfiller().Clan?.Influence;

            if (companionTypes.All(ct => gold < ct.GoldCost && influence < ct.InfluenceCost))
            {
                failedReasons.Add(new TextObject("{!=}You can't afford any companion."));
            }

            return true;
            //return failedReasons.IsEmpty();
        }

        internal override Hero GetFulfiller()
        {
            return Hero.MainHero;
        }

        internal override void ShowInquiry()
        {
            IsFulfilled(out var failedReasons);

            var gold = GetFulfiller().Gold;
            var influence = GetFulfiller().Clan?.Influence;

            var options = new List<InquiryElement>();
            foreach (var companionType in companionTypes)
            {
                var enabled = gold >= companionType.GoldCost && influence >= companionType.InfluenceCost;
                var hint = companionType.Description;

                var template = GetAdequateCharacter(companionType);
                if (template is null) 
                {
                    enabled = false;
                    hint = new TextObject("{=!}No candidates of this type available.").ToString();
                }
                else if (!enabled)
                {
                    hint = failedReasons[0].ToString();
                }

                options.Add(new InquiryElement(companionType, companionType.Name, null, enabled, hint));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=!}Companions").ToString(),
                new TextObject("{=!}Choose a companion to recruit.").ToString(),
                options, 
                true, 
                1, 
                GameTexts.FindText("str_done").ToString(),
                GameTexts.FindText("str_cancel").ToString(),
                delegate (List<InquiryElement> selectedOptions)
                {
                    selectedCompanionType = (CompanionType)selectedOptions.First().Identifier;
                    ApplyGoal();
                }, 
                null, 
                string.Empty));
        }

        private CharacterObject GetAdequateCharacter(CompanionType type)
        {
            var possibleTemplates = new List<(CharacterObject template, float weight)>();
            foreach (var template in GetFulfiller().Culture.NotableAndWandererTemplates.Where(t => t.Occupation == Occupation.Wanderer))
            {
                var weight = 0f;

                foreach (var trait in type.Traits.Where(trait => template.GetTraitLevel(trait) >= 1))
                {
                    weight += template.GetTraitLevel(trait);
                }

                foreach (var perk in type.Perks.Where(perk => template.GetPerkValue(perk)))
                {
                    weight++;
                }

                foreach (var skill in type.Skills.Where(skill => template.GetSkillValue(skill) >= 50))
                {
                    weight += (int)(template.GetSkillValue(skill) / 10f);
                }

                if (weight > 1f)
                {
                    possibleTemplates.Add(new (template, weight));
                }
            }

            possibleTemplates = possibleTemplates.OrderByDescending(pt => pt.weight).ToList();

            return possibleTemplates.Any()
                ? possibleTemplates.First().template
                : null;
        }

        internal override void ApplyGoal()
        {
            var hero = GetFulfiller();
            var characterTemplate = GetAdequateCharacter(selectedCompanionType);

            var possibleEquipmentRosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>()
                .Where(e => e.EquipmentCulture == hero.Culture)
                .ToList();

            var equipmentRoster = possibleEquipmentRosters.Where(e => e.EquipmentCulture == hero.Culture).ToList().GetRandomElementWithPredicate(x => x.StringId.Contains("bannerkings_companion"))
                                  ?? possibleEquipmentRosters.Where(e => e.EquipmentCulture == hero.Culture).ToList().GetRandomElementWithPredicate(x => x.HasEquipmentFlags(EquipmentFlags.IsMediumTemplate));


            var bornSettlement = Settlement.All.GetRandomElementWithPredicate(s => s.Culture == hero.Culture) 
                                 ?? hero.Clan.Settlements.GetRandomElement() 
                                 ?? Settlement.All.GetRandomElement();

            var companion = HeroCreator.CreateSpecialHero(characterTemplate, bornSettlement, null, null, Campaign.Current.Models.AgeModel.HeroComesOfAge + MBRandom.RandomInt(12));
            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, equipmentRoster.AllEquipments.GetRandomElement());
            companion.CompanionOf = hero.Clan;

            var companionFoundMessage = new TextObject("{COMPANION.LINK} was discovered and joined you as companion.");
            companionFoundMessage.SetCharacterProperties("COMPANION", companion.CharacterObject);
            InformationManager.ShowInquiry
            (
                new InquiryData
                (
                    "Companion Recruitment",
                    companionFoundMessage.ToString(),
                    true, 
                    false, 
                    GameTexts.FindText("str_accept").ToString(), 
                    null, 
                    null, 
                    null
                ),
                true
            );
        }

        public override void DoAiDecision()
        {
            throw new NotImplementedException();
        }

        private class CompanionType
        {
            public CompanionType(string stringId, string name, string description, int goldCost, int influenceCost, List<TraitObject> traits, List<PerkObject> perks, List<SkillObject> skills)
            {
                StringId = stringId;
                Name = name;
                Description = description;
                GoldCost = goldCost;
                InfluenceCost = influenceCost;
                Traits = traits;
                Perks = perks;
                Skills = skills;
            }

            public string StringId { get; set; }

            public string Name { get; set; } 

            public string Description { get; set; } 

            public int GoldCost { get; set; }

            public int InfluenceCost { get; set; }

            public List<TraitObject> Traits { get; set; }

            public List<PerkObject> Perks { get; set; }

            public List<SkillObject> Skills { get; set; }
        }
    }
}