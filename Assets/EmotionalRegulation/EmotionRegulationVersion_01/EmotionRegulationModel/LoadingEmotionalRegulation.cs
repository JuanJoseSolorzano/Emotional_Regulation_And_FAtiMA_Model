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
using System.Diagnostics;


namespace EmotionRegulation.Components
{
    internal class LoadingEmotionalRegulation
    {
        public Name EventMatchingTemplate { get; private set; }
        public bool IsSpeak { get; set; }
        public bool IsAvoided { get; private set; }
        public List<AppraisalRuleDTO> EventsToAvoid { get; private set; }
        public bool ExistActionsForSituation { get; private set; }
        public bool ExistEventsForAttention { get; private set; }
        public bool ExistEventsForReappraisal { get; private set; }
        internal List<AppraisalRuleDTO> AppraisalRulesOfEvent { get; private set; }
        public DialogueStateActionDTO  DialogOfEvent { get; set; }
        public EmotionInformation EmotionInformation { get; set; }

        internal RequiredData requiredData;
        internal BaseAgent baseAgent;
        internal RolePlayCharacterAsset _rpc;
        internal IAction decision;

        /// <summary>
        /// Gets all data established.
        /// </summary>
        /// <returns></returns>
        public RequiredData GetData()
        {
            if (requiredData is null)
                throw new ArgumentNullException("The data has not yet been established", nameof(requiredData));
            return requiredData;
        }


        /// <summary>
        /// Se verifican si será posible aplicar alguna estrategia de regualción emocinal.
        /// </summary>
        /// <param name="baseAgent"></param>
        /// <param name="decision"></param>
        /// <returns></returns>
        internal bool ApplyEmotionRegulation(BaseAgent baseAgent, IAction decision)
        {
            ///-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            /// Paso 2.2:
            ///     De la interfaz IAction se deriban dos tipos de acciones, pueden ser de tipo Speak, es decir, los eventos son 
            ///     declarados a través de la clase DialogStateAction, son tratados diferente dentro de ambas arquitecturas.
            ///-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

            /// Se crea un RPC auxiliar para calucalar le emoción.
            _rpc = baseAgent.FAtiMACharacter;
            this.decision = decision;
            IsSpeak = decision.Key == (Name)"Speak";
            bool AnyNegativeEmotion = false;

            /// No creo que sea lo más eficiente y obtimo....

            baseAgent.auxCharacter.Mood = baseAgent.FAtiMACharacter.Mood;

            var eventName = EventHelper.ActionEnd(baseAgent.auxCharacter.CharacterName, decision.Name, decision.Target);
            baseAgent.auxCharacter.Perceive(eventName);
            var emotions = baseAgent.auxCharacter.GetAllActiveEmotions();
            uint evtId = 0;
            foreach (var emotion in emotions)
            {
                evtId = emotion.CauseEventId;
                var OCCemotion = OCCEmotionType.Parse(emotion.Type);
                var appVarOfEmotion = OCCemotion.AppraisalVariables;
                var valence = OCCEmotionType.Parse(emotion.Type).Valence;

                if (valence.Equals(EmotionValence.Negative))
                {

                    ///-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+
                    /// Si entra aquí significa que las nuevas emociones generadas tienen una valencia negativa, por 
                    /// lo tanto se procede cargar la información necesaria para iniciar con el proceso de regulación.
                    ///-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+

                    requiredData = baseAgent.RequiredData;
                    if (CheckDataInformation(decision.Name, IsSpeak))
                    {
                        // Se verifica que existan datos para poder regualar la emoción ocacionda por el evento y se
                        // carga la información necesaria.

                        this.baseAgent = baseAgent;
                        EventMatchingTemplate = AppraisalRulesOfEvent.Select(evt => evt.EventMatchingTemplate).First();
                        var currentStateOfDialog = decision.Parameters;
                        if (IsSpeak)
                        {
                            string utterance = null;
                            DialogOfEvent = requiredData.IAT_FAtiMA.GetDialogAction(decision, out utterance);
                        }
                        EventMatchingTemplate = AppraisalRulesOfEvent.Select(evt => evt.EventMatchingTemplate).First();
                        EmotionInformation emoInfo = new EmotionInformation();
                        var specificAppVar = new List<AppraisalVariableDTO>();
                        foreach (var app in appVarOfEmotion)
                        {
                            specificAppVar.Add(AppraisalRulesOfEvent.Select(v => v.AppraisalVariables.appraisalVariables.Find(v1 =>
                            v1.Name == app)).FirstOrDefault());
                        }
                        emoInfo.SpecificAppVariables = specificAppVar;
                        emoInfo.EmotionDTO = emotion;
                        emoInfo.OCCEmotionType = OCCemotion;
                        emoInfo.CopySpecificAppraisalVariables();
                        EmotionInformation = emoInfo;

                        AnyNegativeEmotion = true;
                        
                    }
                    
                }
            }
            baseAgent.auxCharacter.Mood = 0;

            ///-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+
            /// Si el valor que se retorna es verdadero, ya se habrá cargado toda la información necesaría para
            /// iniciar el proceso de reguación, de lo contrario, hasta aquí llegará la intervención de la 
            /// arquitectura y se retornará el control a FAtiMA.
            ///-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+
            return AnyNegativeEmotion; 
        }

        /// <summary>
        /// Se revisa los datos que se han puesto en la arquitectura de regulación paradeterminar si se podrá 
        /// continuar con el proceso de regulación emocional.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="isSpeak"></param>
        /// <returns></returns>
        bool CheckDataInformation(Name eventName, bool isSpeak)
        {
            bool applyRegulation = false;

            if (!(requiredData.EventsToAvoid is null))
            {
                IsAvoided = false;

                if (isSpeak)
                {
                    AppraisalRulesOfEvent = requiredData.EventsToAvoid.Where(evtAvoid =>
                     evtAvoid.EventMatchingTemplate.GetNTerm(3).GetFirstTerm() == (Name)"Speak").ToList();
                   
                    IsAvoided = AppraisalRulesOfEvent.Any();

                }
                else
                {
                    AppraisalRulesOfEvent = requiredData.EventsToAvoid.FindAll(evtAvoid =>
                                                               evtAvoid.EventMatchingTemplate.GetNTerm(3) == eventName);
                    IsAvoided = AppraisalRulesOfEvent.Any();
                }
                
            }

            if (!(requiredData.ActionsForEvent is null)) ///Revisar esta parte.
            {
                ExistActionsForSituation = false;

                if (isSpeak)
                {
                    var actionsForEvent = requiredData.ActionsForEvent.Find(actionsFor =>
                     actionsFor.EventName == eventName.GetNTerm(4)).AppraisalRulesOfEvent;
                    if (!(actionsForEvent is null))
                    {
                        ExistActionsForSituation = actionsForEvent.Any();
                        if (ExistActionsForSituation)
                            AppraisalRulesOfEvent = actionsForEvent;
                    }
                }
                else
                {
                    var actionsForEvent = requiredData.ActionsForEvent.Find(evtAct => evtAct.EventName == eventName);
                    if(!(actionsForEvent is null))
                    {
                        ExistActionsForSituation = actionsForEvent.AppraisalRulesOfEvent.Any();
                        if (ExistActionsForSituation)
                            AppraisalRulesOfEvent = actionsForEvent.AppraisalRulesOfEvent;
                    }

                }

            }
            //var PastEvents = character.EventRecords.Where(pastEvents => pastEvents.Subject == decision.Target.ToString());
            ExistEventsForAttention = false;
            Debug.Print("debug here L259" + this);

            var allEvents = _rpc.EventRecords;
            Debug.Print("debug here L259" + this);


            if (IsAvoided || ExistActionsForSituation) applyRegulation = true;

            return applyRegulation;
        }

        /// <summary>
        /// Se actualizan unicamente las varibles de valoración identificadas como disparadoras de emociones negativas, las demás 
        /// variables se dejan con los mismos valores. (No sé si se seguirá usando)
        /// </summary>
        /// <param name="oldAppRules">Todas las reglas de valoración que tiene el evento en cuestión</param>
        /// <param name="NewValue">Nuevo valor para las variable(s) que se serán actualizadas</param>
        /// <param name="NewEMT">Nuevo evento</param>
        /// <returns></returns>
        internal AppraisalRuleDTO SetAppRules2(AppraisalRuleDTO oldAppRules, Name NewValue, Name NewEMT)
        {
            List<AppraisalVariableDTO> NewAppVariables = new List<AppraisalVariableDTO>();
            var OldAppVariables = oldAppRules.AppraisalVariables.appraisalVariables;
            List<AppraisalVariableDTO> specifics = new List<AppraisalVariableDTO>();

            foreach (var sp in OldAppVariables)//AppraisalVariablesOfEvent (ESTE ERA EL PARAMETRO ORIGINAL)
            {
                specifics.Add(OldAppVariables.Find(v => v.Name.Equals(sp.Name)));
                OldAppVariables.Remove(sp);
            }
            foreach (var appVar in specifics)
            {
                var oldTarget = appVar.Target;
                var oldVariableName = appVar.Name;
                var NewAppraisalVariableDTO = new AppraisalVariableDTO() { Name = oldVariableName, Target = oldTarget, Value = NewValue };
                NewAppVariables.Add(NewAppraisalVariableDTO);
            }
            foreach (var old in OldAppVariables)
            {
                NewAppVariables.Add(old);
            }
            var rule = new AppraisalRuleDTO()
            {
                EventMatchingTemplate = NewEMT,
                AppraisalVariables = new AppraisalVariables(NewAppVariables)
            };
            return rule;
        }

        /// <summary>
        /// Caso especial para la primera estrategia, pues si el evento no se sucede en el escenario ninguna emoción 
        /// se disparará, ya sea positiva o negativa. Solo se está usando ahí, creo que se puede evitar su uso.
        /// </summary>
        /// <param name="oldAppRules"></param>
        /// <param name="NewValue"></param>
        /// <param name="NewEMT"></param>
        /// <returns></returns>
        internal AppraisalRuleDTO SetAppRules(AppraisalRuleDTO oldAppRules, Name NewValue, Name NewEMT)
        {
            List<AppraisalVariableDTO> NewAppVariables = new List<AppraisalVariableDTO>();
            var OldAppVariables = oldAppRules.AppraisalVariables.appraisalVariables;
            List<AppraisalVariableDTO> specifics = new List<AppraisalVariableDTO>();
            foreach (var appVar in OldAppVariables)
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

    internal class EmotionInformation
    {
        //public List<AppraisalVariableDTO> AppraisalVariablesOfEvent { get; set; }
        public List<AppraisalVariableDTO> SpecificAppVariables { get; set; }
        public List<AppraisalVariableDTO> SpecificAppVariablesCopy { get; set; }
        public EmotionDTO EmotionDTO { get; set; }
        public OCCEmotionType OCCEmotionType { get; set; }

        public void CopySpecificAppraisalVariables()
        {
            List<AppraisalVariableDTO> specificAppVariablesCopy = new List<AppraisalVariableDTO>();
            foreach(var currentAppVar in SpecificAppVariables)
            {
                specificAppVariablesCopy.Add(new AppraisalVariableDTO
                {
                    Name = currentAppVar.Name,
                    Value = currentAppVar.Value,
                    Target = currentAppVar.Target
                });
            }
            SpecificAppVariablesCopy = specificAppVariablesCopy;
        }


    }

}
