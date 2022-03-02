using System;
using System.Collections.Generic;
using System.Text;
using RolePlayCharacter;
using ActionLibrary;
using EmotionRegulation.Components;
using EmotionRegulation.BigFiveModel;
using System.Linq;
using WellFormedNames;
using EmotionalAppraisal.DTOs;
using EmotionalAppraisal;
using System.Diagnostics;

namespace EmotionRegulation
{
    public class EmotionalRegulationAsset
    {
        
        public IAction NewDecision { get; }
        public EmotionRegulationModel.Strategies StrategyApplied { get; }
        public List<string> PossibleEmotions { get; }
        public float possibleIntensity;
        PreparingEmotionRegulationData SetData;
        
        public EmotionalRegulationAsset(RolePlayCharacterAsset character, IAction decision, BaseAgent baseAgent)
        {



        }

        
    }
}
