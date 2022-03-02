using System;
using System.Collections.Generic;
using EmotionRegulation.Components;
using RolePlayCharacter;
using WellFormedNames;
using ActionLibrary;


namespace EmotionRegulation.BigFiveModel
{
    [Serializable]
    public class BaseAgent
    {
        
        public RolePlayCharacterAsset FAtiMACharacter { get; internal set; }
        public Name AgentName { get; private set; }
        public string DominantPersonality { get; private set; }
        public List<KeyValuePair<string, string>> StrategyMetrics { get; private set; }
        public List<string> StrategiesToApply { get; private set; }
        public List<string> AllPersonalities { get; private set; }
        public RequiredData RequiredData { get; set; }

        internal PersonalityDTO personality;
        public BaseAgent(RolePlayCharacterAsset agentFAtiMA, PersonalityDTO personalityDTO, RequiredData info)
        {
 
            FAtiMACharacter = agentFAtiMA;
            RequiredData = info;
            CreateAgente(personalityDTO);

        }
        public BaseAgent() { }

        private void CreateAgente(PersonalityDTO personalityDTO)
        {
            if (RequiredData is null)
                throw new ArgumentNullException(nameof(RequiredData));
            else
            {
                AgentName = FAtiMACharacter.CharacterName;
                var personality = new Personality(personalityDTO);
                var BigFive = personality.BigFive;
                DominantPersonality = BigFive.DominantPersonality;
                StrategiesToApply = BigFive.StrategiesToApply;
                AllPersonalities = BigFive.AllPersonalities;
                StrategyMetrics = BigFive.StrategyMetrics;
                this.personality = personalityDTO;

            }
        }

        public IAction Regulates(IAction decision)
        {
            if (decision is null)
                return null;
            if (this is null)
                throw new ArgumentException("The new agent is null", nameof(BaseAgent));
            if (FAtiMACharacter is null)
                throw new ArgumentException("The character is null", nameof(FAtiMACharacter));
            /// Paso 2.1.- Parte del segundo paso consiste en verificar si la posible decisión provocará una emoción negativa,
            /// para eso es necesario calcular la emoción:
            /// 
            var data = new PreparingEmotionRegulationData(); 
            // Cuando se inicializa éste método, se cargan los primeros datos, en este caso se carga las reglas de valoración
            // del evento.
            var negative = data.GetEmotions(FAtiMACharacter, decision);

  
            if (negative) 
            {
                data.SetDataInformation(this, decision);
                var results = new EmotionRegulationModel(decision, data);
                return results.newAction;
            }
            else
                return null;
        }

        
    }
}
