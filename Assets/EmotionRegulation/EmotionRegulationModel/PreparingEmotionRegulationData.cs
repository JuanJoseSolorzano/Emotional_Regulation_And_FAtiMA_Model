using System;
using System.Collections.Generic;
using System.Linq;
using ActionLibrary;
using WellFormedNames;
using EmotionalAppraisal;
using RolePlayCharacter;
using EmotionalAppraisal.OCCModel;
using EmotionalAppraisal.DTOs;
using AutobiographicMemory;
using AutobiographicMemory.DTOs;
using EmotionRegulation.BigFiveModel;
using IntegratedAuthoringTool;
using IntegratedAuthoringTool.DTOs;

namespace EmotionRegulation.Components
{
    internal class PreparingEmotionRegulationData
    {
        public Name EventMatchingTemplate { get; private set; }
        public List<LikelyEmotion> PossibleEmotion { get; internal set; }
        public bool IsNegativeEmotion { get; private set; }
        public bool IsSpeak { get; set; }
        public bool IsAvoided { get; private set; }
        public bool ExistActionsFor { get; private set; }
        public bool ExistEventsForAttention { get; private set; }
        public bool ExistEventsForReappraisal { get; private set; }
        internal List<AppraisalRuleDTO> AppraisalRulesOfEvent { get; private set; }
        public List<DialogueStateActionDTO>  DialogsOfEvent { get; set; }

        Name decisionName;
        internal BaseAgent baseAgent;
        internal RolePlayCharacterAsset character;
        internal RequiredData requiredData;
        internal float MoodForER;

        internal struct LikelyEmotion
        {
            internal IEmotion EmotionType;
            internal float Intensity;
            internal float Mood;
            internal List<KeyValuePair<string, (float value, EmotionValence valance)>> AppraisalVariables;
        }

        public PreparingEmotionRegulationData() { }

        public RequiredData GetData()
        {
            return requiredData;
        }

        internal PreparingEmotionRegulationData SetDataInformation(BaseAgent dataAgent, IAction decision)
        {
            if (dataAgent is null)
            {
                throw new ArgumentNullException(nameof(dataAgent));
            }

            if (decision is null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            decisionName = decision.Name;
            baseAgent = dataAgent;
            character = dataAgent.FAtiMACharacter;
            requiredData = dataAgent.RequiredData;
            MoodForER = character.Mood;


            checkDataInformation();
            return this;
        }
        internal IEmotion CheckTriggerEmotion(IAction decision, RolePlayCharacterAsset rpc, IntegratedAuthoringToolAsset iat)
        {
            IsSpeak = decision.Key == (Name)"Speak";
            Name style = Name.NIL_SYMBOL;
            var emotionalAppraisal = rpc.m_emotionalAppraisalAsset.GetAllAppraisalRules().ToList();
            List<AppraisalRuleDTO> appRulesOfEvt = new List<AppraisalRuleDTO>();
            List<AppraisalRuleDTO> allAppRules = new List<AppraisalRuleDTO>();

            if (IsSpeak)
            {
                var currentState = decision.Name.GetNTerm(1).ToString();
                var nextState = decision.Name.GetNTerm(2).ToString();
                var meaning = decision.Name.GetNTerm(3);
                style = decision.Name.GetNTerm(4);
                DialogsOfEvent = iat.GetDialogueActions((Name)currentState, (Name)nextState, meaning, style);
                allAppRules = emotionalAppraisal.Where(r => r.EventMatchingTemplate.GetNTerm(3).GetFirstTerm() == (Name)"Speak").ToList();
            }
            else
                allAppRules = emotionalAppraisal;

            foreach (var appRule in allAppRules)
            {
                EventMatchingTemplate = appRule.EventMatchingTemplate;
                /// checks if the actions rule is a dialigue or not
                var actionName = EventMatchingTemplate.GetNTerm(3);
                if (IsSpeak && style.Equals(actionName.GetNTerm(4)))
                {
                    appRulesOfEvt.Add(appRule);

                    break;
                }
                else if (actionName.Equals(decision.Name))
                {
                    appRulesOfEvt.Add(appRule);


                }
            }
            if (appRulesOfEvt.Count == 0)
                throw new IndexOutOfRangeException("List of appraisal rules are empty");


            AppraisalRulesOfEvent = appRulesOfEvt;
            var emotion = EmotionsDerivator(rpc, appRulesOfEvt);

            return emotion.FirstOrDefault(e => e.EmotionType.Valence == EmotionValence.Negative).EmotionType;
        }
        void checkDataInformation()
        {
            if (!(requiredData.EventsToAvoid is null))
            {
                foreach (var appRule in AppraisalRulesOfEvent)
                    IsAvoided = requiredData.EventsToAvoid.Exists(e => e.EventMatchingTemplate == appRule.EventMatchingTemplate);

            }
            if (!(requiredData.ActionsForEvent is null))
            {
                ExistActionsFor = requiredData.ActionsForEvent.ActionName == decisionName.ToString();
            }
            if (!(requiredData.EventsToReappraisal is null))
            {
                ExistEventsForReappraisal = requiredData.EventsToReappraisal.Exists(e1 => e1.GetNTerm(4) == decisionName);
            }
        }

        internal bool GetEmotions(RolePlayCharacterAsset rpc, IAction decision)
        {
            var holdingMood = rpc.Mood;
            var holdingEmotions = rpc.GetAllActiveEmotions();
            bool anyNegative = false;
            var oldlastEmotion = holdingEmotions.LastOrDefault(); ///Si es null significa que no hay emociones previas.

            var eventName = EventHelper.ActionEnd(rpc.CharacterName, decision.Name, decision.Target);
            rpc.Perceive(eventName);
            
            var newEmotions = rpc.GetAllActiveEmotions();
            if (newEmotions.Count() == 0) return false; ;

            var newLastEmotion = newEmotions.LastOrDefault();

            if (oldlastEmotion is null)
            {
                /// Si entra aquí significa que las nuevas emociones generadas son debido al evento que se está analizando.
               
                rpc.Mood = holdingMood; // Recuperamos el antiguo mood.
                foreach (var emotion in newEmotions) // Buscamos si existe una emoción negativa ocacionada por el evento.
                {
                    var emo = OCCEmotionType.Parse(emotion.Type);
                    var appVariablesOfEmotion = emo.AppraisalVariables; // Una emoción puede estár constituida por más de una variable de valoración.
                    var valence = OCCEmotionType.Parse(emotion.Type).Valence; 
                    if (valence.Equals(EmotionValence.Negative))
                    {// Si existe emoción negativa, lo siguiente es recuperar las variables de valoración qué la causan.
                        AppraisalRulesOfEvent = rpc.m_emotionalAppraisalAsset.GetAllAppraisalRules().Where(app => 
                                                            app.EventMatchingTemplate.GetNTerm(3) == decision.Name).ToList();
                        anyNegative = true;
                    }

                    rpc.RemoveEmotion(emotion);
                }
                rpc.ForgetEvent(newLastEmotion.CauseEventId);
            }
            else
            {
                // Sí entra aquí quiere decir que ya existian emociones previas al evento que se está analizando, por lo que hay 
                // que buscar cuáles son.
                var LastEmotions = holdingEmotions.Where(e => e.CauseEventId == oldlastEmotion.CauseEventId).ToList();

                rpc.Mood = holdingMood; // Recuperamos el antiguo mood.
                foreach (var emotion in newEmotions) // Borramos ahora todas las nuevas emociones.
                {

                    var valence = OCCEmotionType.Parse(emotion.Type).Valence;
                    if (valence.Equals(EmotionValence.Negative))
                        anyNegative = true;

                    rpc.RemoveEmotion(emotion);
                }
                rpc.ForgetEvent(newLastEmotion.CauseEventId);

            }
            return anyNegative;
        }

        internal List<LikelyEmotion> EmotionsDerivator(RolePlayCharacterAsset agent, List<AppraisalRuleDTO> currentRules)
        {

            var fatimaConfg = new EmotionalAppraisalConfiguration();
            var completeEventName = currentRules.FirstOrDefault().EventMatchingTemplate;

            List<LikelyEmotion> emotionsOfEvent = new List<LikelyEmotion>();
            List<KeyValuePair<string, (float value, EmotionValence valance)>> keyValuePairs = new List<KeyValuePair<string, (float value, EmotionValence valance)>>();
            LikelyEmotion baseEmotion = new LikelyEmotion() { AppraisalVariables = keyValuePairs };
            
            var agentMood = agent.Mood;

            var FAtiMAconfigs = fatimaConfg;
            var EmotionIntensity = new OCCAffectDerivationComponent();
            var ERframe = new EmotionRegulationFrame();
            var Intensity = 0f;
            var value = 0f;

            ERframe.EmotionRegulationGetAppRules(ERframe, currentRules);
            var BASE_EVENT = new EventByRegulation(0, completeEventName, 0);
            ERframe.AppraisedEvent = BASE_EVENT;
            var emotions = EmotionIntensity.AffectDerivation(agent.m_emotionalAppraisalAsset, null, ERframe);

            //if (EmotionalStateCharacter.Mood != 0) { ER_Mood = EmotionalStateCharacter.Mood; } else { ER_Mood = 0; }

            foreach (var emotion in emotions)
            {
                var CurrentAppraisals = emotion.AppraisalVariables.ToList();
                List<KeyValuePair<string, (float value, EmotionValence valance)>> appraisalvariables = new List<KeyValuePair<string, (float value, EmotionValence valance)>>();
                CurrentAppraisals.ForEach(c =>
                    appraisalvariables.Add(ERframe.RegulationAppraisalVariables.FirstOrDefault(app => app.Key == c)));

                var (_Intensity, Mood) = Determinepotential(emotion);
                MoodForER = Mood;

                baseEmotion.Mood = MoodForER;
                baseEmotion.EmotionType = emotion;
                baseEmotion.Intensity = _Intensity;
                baseEmotion.AppraisalVariables = appraisalvariables;

                emotionsOfEvent.Add(baseEmotion);
            }

            (float Intensity, float Mood) Determinepotential(IEmotion emotion)
            {
                float potential = emotion.Potential;
                float scale = (float)emotion.Valence;
                potential += scale * (MoodForER * FAtiMAconfigs.MoodInfluenceOnEmotionFactor);

                if (potential > 0)
                {
                    /// Decay function: decay = exp(Ln(0.5)/15)
                    /// Final Intensity emotion = Potential * Decay, where potetential is:
                    /// potential += valance * (Mood * 0.3); and potential emotion is the appraisal variable.
                    double lambda = Math.Log(FAtiMAconfigs.HalfLifeDecayConstant) / FAtiMAconfigs.EmotionalHalfLifeDecayTime;
                    float decay = (float)Math.Exp(lambda);
                    var FinalIntensityEmotion = potential * decay;

                    FinalIntensityEmotion = FinalIntensityEmotion < 0 ? 0 : (FinalIntensityEmotion > 10 ? 10 : FinalIntensityEmotion);

                    var FinalMood = UpdateMood(scale, potential);

                    return (FinalIntensityEmotion, FinalMood);
                }
                else
                {
                    return (0f, MoodForER);
                }
            }
            float UpdateMood(float scale, float potential)
            {
                value = Intensity + scale * (potential * FAtiMAconfigs.EmotionInfluenceOnMoodFactor);
                Intensity = value;

                return Intensity;
            }
            PossibleEmotion = emotionsOfEvent;
            return emotionsOfEvent;
        }
        
        internal List<LikelyEmotion> EmotionsDerivator(EmotionalAppraisalAsset ea_Agent, Name eventMatchingTemplate, float mood)
        {
            var fatimaConfg = new EmotionalAppraisalConfiguration();

            List<LikelyEmotion> emotionsOfEvent = new List<LikelyEmotion>();
            List<KeyValuePair<string, (float value, EmotionValence valance)>> keyValuePairs = new List<KeyValuePair<string, (float value, EmotionValence valance)>>();
            LikelyEmotion baseEmotion = new LikelyEmotion() { AppraisalVariables = keyValuePairs };

            var appRules = ea_Agent.GetAllAppraisalRules();
            var agentMood = mood;

            var FAtiMAconfigs = fatimaConfg;
            var EmotionIntensity = new OCCAffectDerivationComponent();
            var ERframe = new EmotionRegulationFrame();
            var Intensity = 0f;
            var value = 0f;

            ERframe.EmotionRegulationGetAppRules(ERframe, appRules);
            var BASE_EVENT = new EventByRegulation(0, eventMatchingTemplate, 0);
            ERframe.AppraisedEvent = BASE_EVENT;
            var emotions = EmotionIntensity.AffectDerivation(ea_Agent, null, ERframe);

            //if (EmotionalStateCharacter.Mood != 0) { ER_Mood = EmotionalStateCharacter.Mood; } else { ER_Mood = 0; }

            foreach (var emotion in emotions)
            {
                var CurrentAppraisals = emotion.AppraisalVariables.ToList();
                List<KeyValuePair<string, (float value, EmotionValence valance)>> appraisalvariables = new List<KeyValuePair<string, (float value, EmotionValence valance)>>();
                CurrentAppraisals.ForEach(c =>
                    appraisalvariables.Add(ERframe.RegulationAppraisalVariables.FirstOrDefault(app => app.Key == c)));

                var (_Intensity, Mood) = Determinepotential(emotion);
                MoodForER = Mood;

                baseEmotion.Mood = MoodForER;
                baseEmotion.EmotionType = emotion;
                baseEmotion.Intensity = _Intensity;
                baseEmotion.AppraisalVariables = appraisalvariables;

                emotionsOfEvent.Add(baseEmotion);
            }


            (float Intensity, float Mood) Determinepotential(IEmotion emotion)
            {
                float potential = emotion.Potential;
                float scale = (float)emotion.Valence;
                potential += scale * (MoodForER * FAtiMAconfigs.MoodInfluenceOnEmotionFactor);


                if (potential > 0)
                {
                    /// Decay function: decay = exp(Ln(0.5)/15)
                    /// Final Intensity emotion = Potential * Decay, where potetential is:
                    /// potential += valance * (Mood * 0.3); and potential emotion is the appraisal variable.
                    double lambda = Math.Log(FAtiMAconfigs.HalfLifeDecayConstant) / FAtiMAconfigs.EmotionalHalfLifeDecayTime;
                    float decay = (float)Math.Exp(lambda);
                    var FinalIntensityEmotion = potential * decay;

                    FinalIntensityEmotion = FinalIntensityEmotion < 0 ? 0 : (FinalIntensityEmotion > 10 ? 10 : FinalIntensityEmotion);

                    var FinalMood = UpdateMood(scale, potential);

                    return (FinalIntensityEmotion, FinalMood);
                }
                else
                {
                    return (0f, MoodForER);
                }
            }
            float UpdateMood(float scale, float potential)
            {
                value = Intensity + scale * (potential * FAtiMAconfigs.EmotionInfluenceOnMoodFactor);
                Intensity = value;

                return Intensity;
            }
            PossibleEmotion = emotionsOfEvent;
            return emotionsOfEvent;
        }

        
        internal AppraisalRuleDTO SetAppRules(AppraisalRuleDTO OldAppRule, Name NewValue, Name NewEMT)
        {
            List<AppraisalVariableDTO> NewAppVariables = new List<AppraisalVariableDTO>();
            var occVariables = OldAppRule.AppraisalVariables.appraisalVariables;


            foreach (var appVar in occVariables)
            {
                var oldTarget = appVar.Target;
                var oldVariableName = appVar.Name;
                var NewAppraisalVariableDTO = new AppraisalVariableDTO() { Name = oldVariableName, Target = oldTarget, Value = NewValue };
                NewAppVariables.Add(NewAppraisalVariableDTO);
            }
            var rule = new AppraisalRuleDTO()
            {
                EventMatchingTemplate = NewEMT,
                AppraisalVariables = new AppraisalVariables(NewAppVariables)
            };
            return rule;
        }
        internal AppraisalRuleDTO SetAppRules(string OCCvariable, string target, Name NewValue, Name NewEMT)
        {
            List<AppraisalVariableDTO> NewAppVariables = new List<AppraisalVariableDTO>();

            var NewAppraisalVariableDTO = new AppraisalVariableDTO() 
            {
                Name = OCCvariable, Target = (Name)target, Value = NewValue
            };
            NewAppVariables.Add(NewAppraisalVariableDTO);

            var rule = new AppraisalRuleDTO()
            {
                EventMatchingTemplate = NewEMT,
                AppraisalVariables = new AppraisalVariables(NewAppVariables)
            };
            return rule;
        }
        internal float AppraisalFunction(float valoration, float evaluation, int lim, int valance)
        {
            var lowLim = lim * 0.4;

            var IntensityValance = 1;
            if (valoration < 0)
                IntensityValance = -1;

            float tanh = (float)((Math.Abs(valoration / 2) * Math.Tanh(valance * ((2 * evaluation) - lim) / lowLim)) + (Math.Abs(valoration / 2)));
            var Intensity = IntensityValance * Math.Abs(tanh);

            return Intensity;
        }

    }
    
    #region FAtiMA Utilities
    internal class EmotionRegulationFrame : IAppraisalFrame
    {
        private Dictionary<string, float> appraisalVariables = new Dictionary<string, float>();
        private Dictionary<string, (float value, EmotionValence valance)> ER_appraisalVariables = new Dictionary<string, (float value, EmotionValence valance)>();

        public IBaseEvent AppraisedEvent { get; set; }

        public IEnumerable<string> AppraisalVariables
        {
            get { return this.appraisalVariables.Keys; }
        }

        public bool IsEmpty
        {
            get { return this.appraisalVariables.Count == 0; }
        }


        public Name Perspective
        {
            get;
            set;
        }

        public EmotionRegulationFrame()
        {
            AppraisedEvent = null;
        }


        public float GetAppraisalVariable(string appraisalVariable)
        {
            if (this.appraisalVariables.ContainsKey(appraisalVariable))
                return appraisalVariables[appraisalVariable];
            else return 0f;
        }

        public bool ContainsAppraisalVariable(string appraisalVariable)
        {
            return this.appraisalVariables.ContainsKey(appraisalVariable);
        }

        public void SetAppraisalVariable(string appraisalVariableName, float value)
        {
            this.appraisalVariables[appraisalVariableName] = value;
        }

        public Dictionary<string, (float value, EmotionValence valance)> RegulationAppraisalVariables
        {
            get { return this.ER_appraisalVariables; }
        }


        private void ERsetAppraisalVariable(string appraisalVariableName, float value)
        {
            EmotionValence _valance;
            if (value >= 0)
                _valance = EmotionValence.Positive;
            else _valance = EmotionValence.Negative;


            this.ER_appraisalVariables[appraisalVariableName] = (value, _valance);

        }
        public void EmotionRegulationGetAppRules(EmotionRegulationFrame frame, IEnumerable<AppraisalRuleDTO> activeRules)
        {
            foreach (var rule in activeRules)
            {
                foreach (var appVar in rule.AppraisalVariables.appraisalVariables)
                {
                    float des;
                    if (!float.TryParse(appVar.Value.ToString(), out des))
                    {
                        throw new ArgumentException(appVar.Name + " can only be a float value and it was " + appVar.Value.ToString());
                    }

                    else if (appVar.Name == OCCAppraisalVariables.DESIRABILITY_FOR_OTHER)
                    {
                        frame.SetAppraisalVariable(OCCAppraisalVariables.DESIRABILITY_FOR_OTHER + " " + appVar.Target, des);
                        frame.ERsetAppraisalVariable(OCCAppraisalVariables.DESIRABILITY_FOR_OTHER, des);

                    }

                    else if (appVar.Name == OCCAppraisalVariables.GOALSUCCESSPROBABILITY)
                    {
                        frame.SetAppraisalVariable(OCCAppraisalVariables.GOALSUCCESSPROBABILITY + "" + appVar.Target, des);
                        frame.ERsetAppraisalVariable(OCCAppraisalVariables.GOALSUCCESSPROBABILITY, des);
                    }

                    else if (appVar.Name == OCCAppraisalVariables.PRAISEWORTHINESS && appVar.Target != Name.NIL_SYMBOL && appVar.Target.IsConstant && !appVar.Target.HasSelf())
                    {
                        frame.SetAppraisalVariable(OCCAppraisalVariables.PRAISEWORTHINESS + " " + appVar.Target, des);
                        frame.ERsetAppraisalVariable(OCCAppraisalVariables.PRAISEWORTHINESS, des);
                    }

                    else
                    {
                        frame.SetAppraisalVariable(appVar.Name, des); frame.ERsetAppraisalVariable(appVar.Name, des);
                    }
                }
            }
        }
    }
    internal class EventByRegulation : IBaseEvent
    {
        private HashSet<string> m_linkedEmotions = new HashSet<string>();
        public uint Id { get; protected set; }

        public IEnumerable<string> LinkedEmotions => m_linkedEmotions;

        public Name Type { get; private set; }

        public Name Subject { get; private set; }

        public ulong Timestamp { get; private set; }

        public Name EventName { get; private set; }

        public EventByRegulation(uint id, Name eventName, ulong timestamp)
        {
            Id = id;
            Type = eventName.GetNTerm(1);
            Subject = eventName.GetNTerm(2);
            Timestamp = timestamp;
            EventName = eventName;
        }

        public void LinkEmotion(string emotionType)
        {
            throw new NotImplementedException();
        }

        public EventDTO ToDTO()
        {
            throw new NotImplementedException();
        }


    }

    #endregion
    
}
