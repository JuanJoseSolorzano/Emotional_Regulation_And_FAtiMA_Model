using System;
using System.Collections.Generic;
using System.Linq;
using WellFormedNames;
using EmotionalAppraisal;
using EmotionalDecisionMaking;
using AutobiographicMemory;
using AutobiographicMemory.DTOs;
using EmotionalAppraisal.DTOs;
using FLS;
using RolePlayCharacter;
using System.IO;
using ActionLibrary;
using ActionLibrary.DTOs;
using EmotionRegulation.BigFiveModel;
using EmotionalAppraisal.OCCModel;
using System.Diagnostics;
using IntegratedAuthoringTool.DTOs;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace EmotionRegulation.Components
{
    public class EmotionRegulationModel
    {

        public bool successfulStrategy { get; private set; }
        public Strategies StrategyApplied { get; internal set; }

        public const string SITUATION_SELECTION = "Situation Selection";
        public const string SITUATION_MODIFICATION = "Situation Modification";
        public const string ATTENTION_DEPLOYMENT = "Attention Deployment";
        public const string COGNITIVE_CHANGE = "Cognitive Change";
        public const string RESPONSE_MODULATION = "Response Modulation";

        internal List<PreparingEmotionRegulationData.LikelyEmotion> possibleEmotions;
        internal IAction newAction;
        PreparingEmotionRegulationData internalSettings;
        RolePlayCharacterAsset character, auxCharacter;
        PersonalityDTO personality;
        Name currentEvent;
        Name currentEventName;
        bool isSpeak;
        BaseAgent agent;
        RequiredData requiredData;
        List<DialogueStateActionDTO> dialogsOfEvent;
        IAction decision;
        


        public enum Strategies
        {
            SituationSelection = 1,
            SituationModification = 2,
            AttentionalDeployment = 3,
            CognitiveChange = 4,
            ResponseModulation = 5
        };

        public EmotionRegulationModel() { }

        internal EmotionRegulationModel(IAction decision, PreparingEmotionRegulationData info)
        {
            this.decision = decision;
            requiredData = info.requiredData;
            this.agent = info.baseAgent;
            isSpeak = info.IsSpeak;
            internalSettings = info;
            character = info.character;
            currentEvent = info.EventMatchingTemplate;
            currentEventName = info.EventMatchingTemplate.GetNTerm(3);
            personality = info.baseAgent.personality;
            possibleEmotions = new List<PreparingEmotionRegulationData.LikelyEmotion>();
            dialogsOfEvent = info.DialogsOfEvent;
            ///Last variable to initialize
            newAction = StartRegulation();
        }





        private IAction StartRegulation()
        {
            var likelyEmotion = internalSettings.PossibleEmotion;

            var appRuleOfEvent = internalSettings.AppraisalRulesOfEvent;
            StrategiesOfAntecedents(appRuleOfEvent, likelyEmotion);
            if (!successfulStrategy)
                StrategiesOfResponse();


            if (successfulStrategy)
            {
                var decision = character.Decide().ToList();
                Debug.Write("\nDebug.... at line 94 "+this);

                if (StrategyApplied == Strategies.SituationSelection)
                {
                    return decision.FirstOrDefault();//the first element works for dialogs, cheking if is different for actions.
                }
                else
                {
                    return decision.FirstOrDefault(d => d.Name == currentEventName);
                }
            }
            else
                return null;
        }

        private void StrategiesOfAntecedents(List<AppraisalRuleDTO> appRuleOfEvent, List<PreparingEmotionRegulationData.LikelyEmotion> emotions)
        {
            var isAvoid = internalSettings.IsAvoided;
            var existsActions = internalSettings.ExistActionsFor;
            var relatedActions = internalSettings.requiredData.ActionsForEvent;
            var existsEvts = internalSettings.ExistEventsForReappraisal;
            var eventsForReappraisal = internalSettings.requiredData.EventsToReappraisal;
            var agentStrategies = agent.StrategiesToApply;
            int detectedStrategy = 0;

            foreach (var strategy in agentStrategies)
            {
                detectedStrategy++;
                if (strategy == SITUATION_SELECTION)
                { successfulStrategy = SituationSelection(isAvoid, appRuleOfEvent); if (successfulStrategy) break; continue; }
                
                else
                if (strategy == SITUATION_MODIFICATION && !successfulStrategy && existsActions)
                { successfulStrategy = SituationModification(emotions, appRuleOfEvent, relatedActions); if (successfulStrategy) break; continue; }
                
                else
                if (strategy == ATTENTION_DEPLOYMENT && !successfulStrategy)
                { successfulStrategy = AttentionDeployment(emotions, appRuleOfEvent); if (successfulStrategy) break; continue; }
               
                else
                if (strategy == COGNITIVE_CHANGE && !successfulStrategy && existsEvts)
                { successfulStrategy = CognitiveChange(emotions, appRuleOfEvent, eventsForReappraisal); if (successfulStrategy) break; continue; }
         
            }
            if(successfulStrategy)
                StrategyApplied = (Strategies)detectedStrategy;
        }

        private void StrategiesOfResponse()
        {
            character.Perceive(currentEvent);
            successfulStrategy = ResponseModulation();
            StrategyApplied = (Strategies)5;
        }

        #region Estrategies of Gross Model

        //Situation Selection 
        private bool SituationSelection(bool isAvoid, List<AppraisalRuleDTO> RulesOfEvent)
        {
            var appliedStrategy = false;

            if (isAvoid)
            {
                if (isSpeak)
                {
                    /// Get the parameters of the current decision name and changes its style
                    var decisionName = decision.Name;
                    var oldStyle = decisionName.GetNTerm(4);
                    var newStyle = "Neutral";
                    var newDecisionName = decisionName.SwapTerms(oldStyle, (Name)newStyle);

                    /// Get the action rule for this event/action or decision, and create new one. Currently, I am assuming there is 
                    /// only one rule.
                    var oldActionRule = character.m_emotionalDecisionMakingAsset.GetAllActionRules().FirstOrDefault(
                        act => act.Action.GetNTerm(4) == oldStyle);

                    var NewPriority = Name.BuildName(float.Parse(oldActionRule.Priority.ToString()) + 1);
                    ActionRuleDTO newActionRule = new ActionRuleDTO()
                    {
                        Action = newDecisionName, ///the actions is the third element of the EventMatchingTemplate.
                        Conditions = oldActionRule.Conditions,
                        Layer = oldActionRule.Layer,
                        Priority = NewPriority,
                        Target = oldActionRule.Target
                    };
                    character.m_emotionalDecisionMakingAsset.AddActionRule(newActionRule);

                    ///get the complete event matching template, and create new one.
                    var eventList = currentEvent.GetLiterals().ToList();
                    var thisStyle = eventList[7];
                    var subject = eventList[2];

                    var newSpeakName = Name.BuildName(String.Concat(
                        eventList[3].ToString() + '(' + eventList[4] + ',' + eventList[5] + ',' + eventList[6] + ',' + newStyle + ')'));

                    var newEventName = Name.BuildName(String.Concat(
                        eventList[0].ToString() + '(' + eventList[1] + ',' + subject + ',' + newSpeakName + ',' + eventList[8]+')'));
                    
                    ///Update de appraisals rules and remove the old one
                    foreach (var appRule in RulesOfEvent)
                    {
                        var newAppRule = internalSettings.SetAppRules(appRule, (Name)"0", newEventName);
                        character.m_emotionalAppraisalAsset.AddOrUpdateAppraisalRule(newAppRule);
                    }
                    character.m_emotionalAppraisalAsset.RemoveAppraisalRules(RulesOfEvent);

                    /// Get all information about the current decision.
                    /// I am assuming that there is only one dialog for the current decicsion.
                    var currentState = dialogsOfEvent.FirstOrDefault().CurrentState;
                    var nextState = dialogsOfEvent.FirstOrDefault().NextState;
                    var meaning = dialogsOfEvent.FirstOrDefault().Meaning;
                    var style = dialogsOfEvent.FirstOrDefault().Style;
                    var utterance = dialogsOfEvent.FirstOrDefault().Utterance;
                    var id = dialogsOfEvent.FirstOrDefault().Id;
                    var utteranceId = dialogsOfEvent.FirstOrDefault().UtteranceId;

                    /// Update the new dialogAction
                    var newUtterance = String.Concat($"Thinking('{utterance}')");
                    DialogueStateActionDTO newDialogStateAction = new DialogueStateActionDTO {
                        CurrentState = currentState,
                        NextState = nextState,
                        Meaning = meaning,
                        Style = newStyle,
                        Utterance = newUtterance,
                        UtteranceId = utteranceId
                    };
                    requiredData.IAT_FAtiMA.AddDialogAction(newDialogStateAction);

                    /// Mark the strategy as succesful
                    appliedStrategy = true;
                }
                else /// If action/event is not a dialog, this block will change the name of the current event.
                {
                    var eventList = currentEvent.GetLiterals().ToList();

                    ///Building new event and adding word NOT at the event
                    eventList.RemoveAt(3);
                    var newEventName = Name.BuildName("Not-" + currentEvent.GetNTerm(3));
                    eventList.Insert(3, newEventName);;

                    var NewEvent = Name.BuildName(eventList);


                    ///Update de appraisals rules

                    foreach (var appRule in RulesOfEvent)
                    {
                        var newAppRule = internalSettings.SetAppRules(appRule, (Name)"0", NewEvent);
                        character.m_emotionalAppraisalAsset.AddOrUpdateAppraisalRule(newAppRule);
                    }

                    ///change the action of agent
                    var currentAction = character.m_emotionalDecisionMakingAsset.GetAllActionRules().FirstOrDefault(
                        act => act.Action == currentEventName);
                    var NewPriority = Name.BuildName(float.Parse(currentAction.Priority.ToString()) * 2);

                    ActionRuleDTO action = new ActionRuleDTO()
                    {
                        Action = newEventName,
                        Conditions = currentAction.Conditions,
                        Layer = currentAction.Layer,
                        Priority = NewPriority,
                        Target = currentAction.Target
                    };

                    character.m_emotionalDecisionMakingAsset.AddActionRule(action);
                    appliedStrategy = true;
                }

                
            }

            return appliedStrategy;

        }

        //Situation Modification 
        private bool SituationModification(List<PreparingEmotionRegulationData.LikelyEmotion> NegativeEmotions, List<AppraisalRuleDTO> RulesOfEvent, ActionsforEvent reactions)
        {
            var AppliedStrategy = false;
            var reaction = reactions.ActionsForEventER; //Mejorar esta parte de la elección de la acción.
            float UnfitPersonality = (float)((personality.Neuroticism + personality.Agreeableness) / 2);
            float FitPersonality = (float)((personality.Conscientiousness + personality.Extraversion + personality.Openness) / 3);
            auxCharacter = new RolePlayCharacterAsset();
            var AuxEA = new EmotionalAppraisalAsset();

            ///intento de ver como mejorar lo de la selección del evento.
            foreach (var eff in reaction)
            {
                float fitPersonalityAux = FitPersonality;
                float unfitPersonlityAux = UnfitPersonality;
                if (eff.Value < 0)
                {
                    var absEff = -1 * eff.Value;
                    var effValue = internalSettings.AppraisalFunction(absEff, FitPersonality, 10, -1);
                    FitPersonality += effValue;
                }
                else
                {
                    
                    var effValue = internalSettings.AppraisalFunction(eff.Value, UnfitPersonality, 10, 1);
                    FitPersonality += effValue;
                }

            }

            foreach (var emotion in NegativeEmotions)
            {
                var _AppraisalVariables = emotion.AppraisalVariables;

                var rules = new List<AppraisalRuleDTO>();
                foreach (var old in _AppraisalVariables)
                {

                    var UnfitTanh = internalSettings.AppraisalFunction(old.Value.value, UnfitPersonality, 100, -(int)old.Value.valance);
                    var FitTanh = internalSettings.AppraisalFunction(old.Value.value, FitPersonality, 100, (int)old.Value.valance);
                    var tanh = (UnfitTanh + FitTanh) / 2;
                    Name _target = Name.NIL_SYMBOL;
                    Name _eventMatchingTemplate = Name.NIL_SYMBOL;

                    foreach(var r1 in RulesOfEvent)
                    {
                        var appraisalVariables = r1.AppraisalVariables.appraisalVariables;
                        _target = appraisalVariables.FirstOrDefault(v1 => v1.Name == old.Key).Target;
                        _eventMatchingTemplate = r1.EventMatchingTemplate;
                    }
                    var appraisalVariableDTO = new List<AppraisalVariableDTO>()
                        {
                            new AppraisalVariableDTO ()
                            {
                            Name = old.Key,
                            Value = Name.BuildName(tanh),
                            Target = _target
                            }
                        };
                    var rule = new AppraisalRuleDTO()
                    {
                        EventMatchingTemplate = _eventMatchingTemplate,
                        AppraisalVariables = new AppraisalVariables(appraisalVariableDTO)
                    };
                    rules.Add(rule);
                    AuxEA.AddOrUpdateAppraisalRule(rule);
                    auxCharacter.m_emotionalAppraisalAsset.AddOrUpdateAppraisalRule(rule);

                }
                var eventName = EventHelper.ActionEnd(character.CharacterName, decision.Name, decision.Target);
                auxCharacter.Perceive(eventName);
                var emo = auxCharacter.GetAllActiveEmotions();
                
                possibleEmotions = internalSettings.EmotionsDerivator(AuxEA, currentEvent, character.Mood); ///emociones potentiales

                var Threshold = false;
                if (possibleEmotions.Any(e => (float)e.EmotionType.Valence == 1))
                {
                    Threshold = true;
                }
                else Threshold = possibleEmotions.FirstOrDefault().Intensity <= personality.MaxLevelEmotion;

                if (Threshold)
                {
                    foreach (var New in possibleEmotions.FirstOrDefault().AppraisalVariables)
                    {
                        RulesOfEvent.ToList().ForEach(r => r.AppraisalVariables.appraisalVariables.FirstOrDefault(
                            v => v.Name == New.Key).Value = Name.BuildName(New.Value.value));
                    }

                    reactions.ActionName = reaction.FirstOrDefault().Key;
                    

                    character.m_emotionalDecisionMakingAsset.AddActionRule(reactions.GetActionRule);

                    AppliedStrategy = true;
                }
    
                }
            return AppliedStrategy;
        }

        //Attention Deployment 
        private bool AttentionDeployment(List<PreparingEmotionRegulationData.LikelyEmotion> NegativeEmotions, List<AppraisalRuleDTO> RulesOfEvent)
        {
            ///Past events, whose target matches with the target of the current event. (T->T)/(4-4)

            var AppliedStrategy = false;
            var PastEvents = character.EventRecords.Where(pastEvents => pastEvents.Subject == currentEvent.GetNTerm(2).ToString()).ToList();
            var AllRulesOfScenario = character.m_emotionalAppraisalAsset.GetAllAppraisalRules().ToList();

            if (PastEvents.Any())
            {
                List<AppraisalRuleDTO> OldappraisalRules = new List<AppraisalRuleDTO>();
                var emotionsOfPastEvt = new List<PreparingEmotionRegulationData.LikelyEmotion>();
                foreach (var RulesOfpastEvents in AllRulesOfScenario)
                {

                    foreach (var Event in PastEvents)
                    {
                        var pastEvt = (Name)Event.Event;
                        
                        if (pastEvt.GetNTerm(3).Equals(RulesOfpastEvents.EventMatchingTemplate.GetNTerm(3)))
                        {
                            OldappraisalRules.Add(RulesOfpastEvents);
                        }
                        else
                            break;
                        emotionsOfPastEvt = internalSettings.EmotionsDerivator(character.m_emotionalAppraisalAsset, pastEvt, character.Mood);
                    }   
                }
                if (!emotionsOfPastEvt.Any() || emotionsOfPastEvt.Where(e => (float)e.EmotionType.Valence == 1) == null)
                {
                    return AppliedStrategy;
                }


                var AuxEA = new EmotionalAppraisalAsset();
                var avg = emotionsOfPastEvt.Select(emo => emo.Intensity).Average(); ///avg de intensidad positiva de los eventos pasados
                foreach (var NegativeEmotion in NegativeEmotions)
                {
                    var AppraisalVariablesInEmotion = NegativeEmotion.AppraisalVariables;
                    var Newrules = new List<AppraisalRuleDTO>();

                    foreach (var old in AppraisalVariablesInEmotion)
                    {
                        var tanh = internalSettings.AppraisalFunction(old.Value.value, avg, 10, (int)old.Value.valance);
                        var _target = string.Empty;
                        var _eventMatchingTemplate = string.Empty;

                        RulesOfEvent.ToList().ForEach(r2 =>
                        {
                            var appraisalVariables = r2.AppraisalVariables.appraisalVariables;
                            _target = appraisalVariables.FirstOrDefault(v => v.Name == old.Key).Target.ToString();
                            _eventMatchingTemplate = r2.EventMatchingTemplate.ToString();
                        });
                        var appraisalVariableDTO = new List<AppraisalVariableDTO>()
                        {
                        new AppraisalVariableDTO ()
                        {
                            Name = old.Key,
                            Value = Name.BuildName(tanh),
                            Target = (Name)_target
                        }
                        };
                        var rule = new AppraisalRuleDTO()
                        {
                            EventMatchingTemplate = (Name)_eventMatchingTemplate,
                            AppraisalVariables = new AppraisalVariables(appraisalVariableDTO)
                        };
                        Newrules.Add(rule);
                        AuxEA.AddOrUpdateAppraisalRule(rule);
                    }
                    var potentialemotions = internalSettings.EmotionsDerivator(AuxEA, currentEvent, character.Mood);
                    var Threshold = potentialemotions.LastOrDefault().Intensity <= personality.MaxLevelEmotion;

                    if (Threshold)
                    {
                        foreach (var New in potentialemotions.FirstOrDefault().AppraisalVariables)
                        {
    
                            RulesOfEvent.ToList().ForEach(r => r.AppraisalVariables.appraisalVariables.FirstOrDefault(
                                v => v.Name == New.Key).Value = Name.BuildName(New.Value.value));
                        }

                        possibleEmotions = potentialemotions;
                        foreach(var NewAppRule in Newrules)
                            character.m_emotionalAppraisalAsset.AddOrUpdateAppraisalRule(NewAppRule);

                        var currentAction = character.m_emotionalDecisionMakingAsset.GetAllActionRules().FirstOrDefault(
                                    act => act.Action == currentEventName);
                        var NewPriority = Name.BuildName(float.Parse(currentAction.Priority.ToString()) * 2);

                        foreach(var pastEvt in PastEvents)
                        {
                            var nameOfEvent = (Name)pastEvt.Event;
                            var avtionName = nameOfEvent.GetNTerm(3);
                            
                            ActionRuleDTO action = new ActionRuleDTO()
                            {
                                Action = Name.BuildName("FocusOn"+avtionName),
                                Conditions = currentAction.Conditions,
                                Layer = currentAction.Layer,
                                Priority = NewPriority,
                                Target = currentAction.Target
                            };

                            character.m_emotionalDecisionMakingAsset.AddActionRule(action);
                        }
                        AppliedStrategy = true;
                    }

                }

            }
            return AppliedStrategy;
        }

        // Cognitive Change
        private bool CognitiveChange(List<PreparingEmotionRegulationData.LikelyEmotion> NegativeEmotions, List<AppraisalRuleDTO> RulesOfEvent, List<Name> evtsForReappraisal)
        {
            Console.WriteLine("\n\n---------------------Cognitive Change------------------------");
            var AppliedStrategy = false;
            
            var AlternativeEvents  = evtsForReappraisal.Where(Event => Event.GetNTerm(4) == currentEventName);

            if (!AlternativeEvents.Any())
            {
                return AppliedStrategy;
            }
            var AllAppRules = character.m_emotionalAppraisalAsset.GetAllAppraisalRules().ToList();
            var EmotionsOfAlternativeEvt = new List<List<PreparingEmotionRegulationData.LikelyEmotion>>();
            var IntensitySum = new List<float>();

            foreach (var alternativeEvent in AlternativeEvents)
            {
                var RulesOfAlternatveEvt = AllAppRules.Where(
                r => r.EventMatchingTemplate.GetNTerm(3) == alternativeEvent.GetNTerm(3)).ToList();
    
                var EmotionsFelt = internalSettings.EmotionsDerivator(character, RulesOfAlternatveEvt);
                EmotionsOfAlternativeEvt.Add(EmotionsFelt);
                //Console.WriteLine("\n '" + alternativeEvent.GetNTerm(3) + "'");
                //EmotionsFelt.ForEach(e => Console.WriteLine($@"     Feeling emotion ---> {e.EmotionType.EmotionType} : {e.Intensity}"));
            }
            EmotionsOfAlternativeEvt.ForEach(ea =>
            { ea.Select(emo => emo.Intensity).ForEach(e => IntensitySum.Add(e)); });

            if (IntensitySum.Any())
            {
                var UnfitPersonality = (float)((personality.Neuroticism + personality.Agreeableness) / 2);
                var FitPersonality = (float)((personality.Conscientiousness + personality.Extraversion + personality.Openness) / 3);
                var ReinterpretationAvg = IntensitySum.Average();

                var AuxEA = new EmotionalAppraisalAsset();
                foreach (var NegativeEmotion in NegativeEmotions)
                {
                    var AppraisalVariablesInEmotion = NegativeEmotion.AppraisalVariables;
                    var rules = new List<AppraisalRuleDTO>();
                    foreach (var old in AppraisalVariablesInEmotion)
                    {
                        var Valoration = (character.Mood + ReinterpretationAvg);
                        var UnfitTanh = internalSettings.AppraisalFunction(old.Value.value, UnfitPersonality, 100, (int)old.Value.valance);
                        var FitTanh =  internalSettings.AppraisalFunction(old.Value.value, FitPersonality, 100, (int)old.Value.valance);
                        var MoodIntensity = internalSettings.AppraisalFunction(old.Value.value, Valoration, 10, (int)old.Value.valance);
                        var tanh = (float)((UnfitTanh + MoodIntensity + FitTanh) / 3);

                        var _target = string.Empty;
                        var _eventMatchingTemplate = string.Empty;

                        RulesOfEvent.ToList().ForEach(r3 =>
                        {
                            var appraisalVariables = r3.AppraisalVariables.appraisalVariables;
                            _target = appraisalVariables.FirstOrDefault(v => v.Name == old.Key).Target.ToString();
                            _eventMatchingTemplate = r3.EventMatchingTemplate.ToString();
                        });

                        var appraisalVariableDTO = new List<AppraisalVariableDTO>()
                        {
                        new AppraisalVariableDTO ()
                            {
                            Name = old.Key,
                            Value = Name.BuildName(tanh),
                            Target = (Name)_target
                            }
                        };
                        var rule = new AppraisalRuleDTO()
                        {
                            EventMatchingTemplate = (Name)_eventMatchingTemplate,
                            AppraisalVariables = new AppraisalVariables(appraisalVariableDTO)
                        };
                        rules.Add(rule);
                        AuxEA.AddOrUpdateAppraisalRule(rule);
                    }
                    var potentialemotions = internalSettings.EmotionsDerivator(AuxEA, currentEvent, character.Mood); ///emociones potentiales

                    Console.WriteLine(" \n Must generate --> "
                        + potentialemotions.FirstOrDefault().EmotionType.EmotionType + " : "
                        + potentialemotions.LastOrDefault().Intensity
                        + " \n                   Mood : " + potentialemotions.LastOrDefault().Mood);

                    var Threshold = false;
                    if (potentialemotions.Where(e => (float)e.EmotionType.Valence == 1).Any())
                        Threshold = true;

                    Threshold = potentialemotions.LastOrDefault().Intensity <= personality.MaxLevelEmotion;
                    if (Threshold)
                    {
                        foreach (var New in potentialemotions.FirstOrDefault().AppraisalVariables)
                        {
                            ///Aquí se están ya actualizando las variables del valoración
                            RulesOfEvent.ToList().ForEach(r => r.AppraisalVariables.appraisalVariables.FirstOrDefault(
                                v => v.Name == New.Key).Value = Name.BuildName(New.Value.value));
                        }
                        possibleEmotions = potentialemotions;

                        var currentAction = character.m_emotionalDecisionMakingAsset.GetAllActionRules().FirstOrDefault(
                                    act => act.Action == currentEventName);
                        var NewPriority = Name.BuildName(float.Parse(currentAction.Priority.ToString()) * 2);

                        foreach (var evt in AlternativeEvents)
                        {
                            
                            var avtionName = evt.GetNTerm(3);

                            ActionRuleDTO action = new ActionRuleDTO()
                            {
                                Action = Name.BuildName("ThinkingIn" + avtionName),
                                Conditions = currentAction.Conditions,
                                Layer = currentAction.Layer,
                                Priority = NewPriority,
                                Target = currentAction.Target
                            };

                            character.m_emotionalDecisionMakingAsset.AddActionRule(action);
                        }

                        AppliedStrategy = true;
                    }
                }
            }
            return AppliedStrategy;
        }
    
        //Response Modulation
        private bool ResponseModulation()
        {
            var AppliedStrategy = false;
            var FAtiMAConfiguration = new EmotionalAppraisalConfiguration();

            var ActiveEmotion = character.GetAllActiveEmotions().LastOrDefault();
            ///Se supone que se está evaluando una emoción negativa, por lo qué el valance simpre será negativo
            int valenceOfemotion = -1;

            var _personalitiesValues = new float[] 
            {
                personality.Openness, 
                personality.Conscientiousness, 
                personality.Extraversion, 
                personality.Agreeableness,
                personality.Neuroticism
            };

            var allTraits = _personalitiesValues.Average();
            var PersonalityBasedEmotion = internalSettings.AppraisalFunction(allTraits, ActiveEmotion.Intensity, 5, 1) + allTraits;
            var newIntensity = (float)Math.Round(internalSettings.AppraisalFunction(ActiveEmotion.Intensity, PersonalityBasedEmotion, 100, -1));

            var Threshold = newIntensity <= personality.MaxLevelEmotion;

            if (Threshold)
            {
                var MoodDueToEvent = valenceOfemotion * (ActiveEmotion.Intensity * FAtiMAConfiguration.MoodInfluenceOnEmotionFactor);
                var valueMoodDueToEvent = MoodDueToEvent < -10 ? -10 : (MoodDueToEvent > 10 ? 10 : MoodDueToEvent);
                var CurrentMoodDueToEvent = character.Mood;
                var MoodWithoutEventValue = CurrentMoodDueToEvent - MoodDueToEvent;

                //To create and update the emotion
                var NewEmotion = new EmotionDTO
                {
                    CauseEventId = ActiveEmotion.CauseEventId,
                    Type = ActiveEmotion.Type,
                    Intensity = newIntensity,
                    CauseEventName = ActiveEmotion.CauseEventName,
                    Target = ActiveEmotion.Target,
                };

                var AuxConcreteEmotion = new ConcreteEmotionalState();
                character.RemoveEmotion(ActiveEmotion); ///Remove the emotion from agent
                character.AddActiveEmotion(NewEmotion);

                var newEmotions = character.GetAllActiveEmotions().ToList();
  
                //Update the Mood
                var NewEmoValence = valenceOfemotion;
                var NewEmoIntencity = newEmotions.LastOrDefault().Intensity;
                var moodDouToNewIntensity = NewEmoValence * (NewEmoIntencity * FAtiMAConfiguration.MoodInfluenceOnEmotionFactor);
                var MoodDouToNewIntensity = moodDouToNewIntensity < -10 ? -10 : (moodDouToNewIntensity > 10 ? 10 : moodDouToNewIntensity);
                var NewMood = MoodWithoutEventValue + MoodDouToNewIntensity;
                character.Mood = NewMood;

                AppliedStrategy = true;
            }

            return AppliedStrategy;
        }

        #endregion
    }

}
