using System;
using System.Collections.Generic;
using System.Linq;
using WellFormedNames;
using EmotionalAppraisal;
using EmotionalDecisionMaking;
using AutobiographicMemory;
using AutobiographicMemory.DTOs;
using EmotionalAppraisal.DTOs;
using EmotionalAppraisal.OCCModel;
using FLS;

namespace EmotionRegulationAsset
{
    public class EmotionRegulationAsset
    {

        public EmotionRegulationAsset() { }
        public EmotionRegulationAsset(EmotionalAppraisalAsset eaCharater,
            ConcreteEmotionalState emotionalStateCharacter, PersonalityTraits Personality,
            Dictionary<string, string> relatedActions, List<Name> AlternativeEvents,
            double ExpectedIntensity, AM am, EmotionalDecisionMakingAsset edm)
        {
            this.eaCharater = eaCharater;
            this.Personality = Personality;
            this.AlternativeEvents = AlternativeEvents;
            this.ExpectedIntensity = ExpectedIntensity;
            this.amCharacter = am;
            this.edmCharacter = edm;

            EmotionalStateCharacter = emotionalStateCharacter;
            ER_Mood = emotionalStateCharacter.Mood;
            EmotionRegulationActions = relatedActions;
            EventToFAtiMA = Name.NIL_SYMBOL;

            IDevent = 0;

            AppliedStrategy = false;
            Target = Name.NIL_SYMBOL;
            EventName = Name.NIL_SYMBOL;
        }

        public const string SITUATION_SELECTION     = "Situation Selection";
        public const string SITUATION_MODIFICATION  = "Situation Modification";
        public const string ATTENTION_DEPLOYMENT    = "Attention Deployment";
        public const string COGNITIVE_CHANGE        = "Cognitive Change";
        public const string RESPONSE_MODULATION     = "Response Modulation";
        public Name EventToFAtiMA;
        
        private InputDataFrame OutputData = new() {
                                                    Results = new List<(string Event, string Strategy)>(), 
                                                    NormalEmotions = new List<double> (),
                                                    NormalMood = new List<double>() };
        private int IDevent = 0;
        private Name Target;
        private Name EventName;
        private float ER_Mood;
        
        private readonly EmotionalAppraisalConfiguration FAtiMAConfiguration = new();
        private readonly EmotionalAppraisalAsset eaCharater;
        private readonly EmotionalDecisionMakingAsset edmCharacter;
        private readonly AM amCharacter;
        private readonly List<Name> AlternativeEvents;
        private readonly ConcreteEmotionalState EmotionalStateCharacter;
        private readonly PersonalityTraits Personality;
        private readonly double ExpectedIntensity;
        private readonly Dictionary<string, string> EmotionRegulationActions;
        private bool AppliedStrategy;

        public struct InputDataFrame
        {
            public bool StrategySuccessful;
            public List<(string Event, string Strategy)> Results;
            public List<double> NormalEmotions;
            public List<double> NormalMood;
        }
        private struct Eemotion
        {
            public IEmotion EmotionType;
            public float Intensity;
            public float Mood;
            public List<KeyValuePair<string, (float value, EmotionValence valance)>> AppraisalVariables;
        }

        //Situation Selection 
        private bool SituationSelection(Name Event, bool IsAvoided)
        {
            Console.WriteLine($"\n\n---------------------{ SITUATION_SELECTION }-------------------------");

            AppliedStrategy = false;
            Console.WriteLine($"\n Event name: {EventName}                         Target: {Target}");
            Console.WriteLine($@" Can be avoided: {IsAvoided}");

            /// summary: 
            ///     Si el usuaria ha confiagurado el evento como "evitable", a saber, colocar la palabra reservada 'true' en el 
            ///     quinto termino del evento, entoces la estrategia podra ser aplicada para el evento en cuestión siempre que el
            ///     agente cumpla con las caracteristicas de personalidad.
            ///     
            if (IsAvoided)
            {
                Console.WriteLine("\n In progress...  ");
                Console.WriteLine(" Evaluating new event...  ");

                var ListEvent = Event.GetLiterals().ToList();
                var EventMatching = eaCharater.GetAllAppraisalRules().FirstOrDefault(
                    i => i.EventMatchingTemplate.GetNTerm(3) == EventName).EventMatchingTemplate.GetLiterals().ToList();

                ///Building new eventTemplate adding word NOT at the event
                ListEvent.RemoveAt(3);
                EventMatching.RemoveAt(3);
                var NewEventName = Name.BuildName("Not-" + EventName);
                EventMatching.Insert(3, NewEventName);
                ListEvent.Insert(3, NewEventName);
                var NewEventMatchingTemplate = Name.BuildName(EventMatching); //new EventMatchingTemplate with NOT word
                EventToFAtiMA = Name.BuildName(ListEvent); //new event with NOT word

                ///Setup the wew Appraisals variables
                var OldsAppraisalVariables = eaCharater.GetAllAppraisalRules().FirstOrDefault(
                    i => i.EventMatchingTemplate.GetNTerm(3) == EventName).AppraisalVariables.appraisalVariables;
                OldsAppraisalVariables.ForEach(app => app.Value = (Name)"0");

                AppliedStrategy = true;
                Console.WriteLine(" \nNew event: " + EventToFAtiMA.GetNTerm(3));
            }
            else { Console.WriteLine("\n Strategy not applied because :\n Event cannot be Avoided : " + !IsAvoided); }

            Console.WriteLine("\nSituation Selection was applied: " + AppliedStrategy);
            Console.WriteLine("-----------------------------------------------------------------\n\n");
            return AppliedStrategy;
        }

        //Situation Modification 
        private bool SituationModification(List<Eemotion> NegativeEmotions, List<AppraisalRuleDTO> RulesOfEvent)
        {
            Console.WriteLine($"\n\n---------------------{ SITUATION_MODIFICATION }-----------------------");
            AppliedStrategy = false;

            var Mood = EmotionalStateCharacter.Mood; ///No recerdo si esta varible se utiliza dentro de este método, revisar para que la
            /// declare en esta parte del código.
            List<KeyValuePair<string, string>> ExistRelatedActions = new();
            if (EmotionRegulationActions == null)
            {
                Console.WriteLine("Doesn't exists any actions  = " + (EmotionRegulationActions == null));
                Console.WriteLine("\nSituation Modification was applied: " + AppliedStrategy);
                Console.WriteLine("---------------------------------------------------------------\n\n");
                return AppliedStrategy;
            }
            else ExistRelatedActions = EmotionRegulationActions.Where(actions => actions.Value == EventName.ToString()).ToList();

            Console.WriteLine($"\n Event name: {EventName}                         Target: {Target}");
            Console.WriteLine($" Agent could take any action ? : {ExistRelatedActions.Any()}");

            if (ExistRelatedActions.Any())
            {
                #region Actions
                ///Parte donde se búscan las acciones relacionadas.
                var RelatedActionsName = ExistRelatedActions.Select(a => a.Key).ToList();
                List<ActionLibrary.DTOs.ActionRuleDTO> ActionsToDo = new();

                foreach (var action in RelatedActionsName)
                {
                    ///find the actions matching with the dictionary EmotionRegulationActions.
                    ActionsToDo.Add(edmCharacter.GetAllActionRules().FirstOrDefault(a => a.Action.ToString() == action));
                }

                var RelatedActions = edmCharacter.GetAllActionRules().Where(///Hay que saber si esto es parte del código anterior.
                    a => a.Action.ToString() == ExistRelatedActions.Select(ea => ea.Key).FirstOrDefault());

                Console.WriteLine(" \n In progress...  ");
                Console.WriteLine(" Evaluating actions...  ");
                #endregion

                /// Sumary:
                ///     Suponiendo que las diferentes acciones generarán diferentes niveles de emoción en el agente, se calculará
                ///     un nivel de intensidad de acuerdo a la acción elegida. PD: Lógicamente, no sería posible conocer el nivel 
                ///     de intensidad de la emoción debido a una acción hasta que ésta se genere, algo así como un tipo de caja 
                ///     negra. Pero aqui todavia no se sabe que acción será elegida por el usuario, por lo tanto este paso es solo
                ///     una aproximación, habra que verificar con el RoolePlayCharacter.
                ///     Cómo valorar las diferentes acciones.. con base a qué se puede calcular las nuevas variables de valoración 
                ///     actualmente no se tiene niguna distinción entre las diferentes acciones que se puedan ejucutar.
                ///     Para resolver esto es necesario conocer como funciona AuthoringToolkit and RoolPlayCharacter.
                /// Summary:
                ///     Aquí se está enviando la información para calcular el nuevo valor de las variables de valoración
                ///     mediante la función establecida.
                ///   

                ActionsToDo.ForEach(action => Console.WriteLine($" Agent would decide to do : {action.Action}"));
                var UnfitPersonality = (float)((Personality.Neuroticism + Personality.Agreeableness) / 2);
                var FitPersonality = (float)((Personality.Conscientiousness + Personality.Extraversion + Personality.Openness) / 3);

                var AuxEA = new EmotionalAppraisalAsset();
                foreach (var emotion in NegativeEmotions)
                {
                    var _AppraisalVariables = emotion.AppraisalVariables;

                    var rules = new List<AppraisalRuleDTO>();
                    foreach (var old in _AppraisalVariables)
                    {
                        var UnfitTanh = AppraisalFunction(old.Value.value, UnfitPersonality, 100, -(int)old.Value.valance);
                        var FitTanh = AppraisalFunction(old.Value.value, FitPersonality, 100, (int)old.Value.valance);
                        var tanh = (UnfitTanh + FitTanh) / 2;
                        var _target = string.Empty;
                        var _eventMatchingTemplate = string.Empty;

                        RulesOfEvent.ToList().ForEach(rule =>
                        {
                            var appraisalVariables = rule.AppraisalVariables.appraisalVariables;
                            _target = appraisalVariables.FirstOrDefault(v => v.Name == old.Key).Target.ToString();
                            _eventMatchingTemplate = rule.EventMatchingTemplate.ToString();
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
                    var potentialemotions = EmotionsDerivator(AuxEA, rules, EventToFAtiMA); ///emociones potentiales

                    var Threshold = false;
                    if (potentialemotions.Any(e => (float)e.EmotionType.Valence == 1))
                    {
                        Threshold = true;
                    }
                    else Threshold = potentialemotions.FirstOrDefault().Intensity <= ExpectedIntensity;

                    Console.WriteLine(" Must generate --> " +
                        potentialemotions.FirstOrDefault().EmotionType.EmotionType + " : " +
                        potentialemotions.LastOrDefault().Intensity
                        + " \n                   Mood : " + potentialemotions.LastOrDefault().Mood);

                    if (Threshold)
                    {
                        foreach (var New in potentialemotions.FirstOrDefault().AppraisalVariables)
                        {
                            RulesOfEvent.ToList().ForEach(r => r.AppraisalVariables.appraisalVariables.FirstOrDefault(
                                v => v.Name == New.Key).Value = Name.BuildName(New.Value.value));
                        }
                        AppliedStrategy = true;///Si una emoción negativa falla??, la cuestión es, se asume qué un evento solo 
                                               ///puede generar un solo tipo de emoción, sin embargo FAtiMA permite generar varias emociones en un mismo evento, por
                                               ///eso fue necesario reajustar todo el código. Por ahora ya solo faltaría revisar eso de la multiple valoración de un
                                               ///mismo evento.
                    }
                    else
                    {
                        Console.WriteLine("\n Strategy has not applied because : Intensity threshold failed = " + !Threshold);
                        Console.WriteLine($" New possible intensity = {23}, (User defined limit = {ExpectedIntensity})");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n Strategy has not applied because :\n " +
                                                        "Doesn't exists Related Actions = " + !ExistRelatedActions.Any());
            }
            Console.WriteLine("\nSituation Modification was applied: " + AppliedStrategy);
            Console.WriteLine("---------------------------------------------------------------\n\n");
            return AppliedStrategy;
        }

        //Attention Deployment 
        private bool AttentionDeployment(List<Eemotion> NegativeEmotions, List<AppraisalRuleDTO> RulesOfEvent)
        {
            Console.WriteLine("\n\n---------------------Attention Deployment------------------------");
            AppliedStrategy = false;
            Console.WriteLine($"\nEvent name: {EventName}                           Target: {Target}");

            ///Past events, whose target matches with the target of the current event. (T->T)/(4-4)
            var PastEvents = amCharacter.RecallAllEvents().Where(pastEvents
                              => pastEvents.EventName.GetNTerm(4) == Target).Select(IEvent => IEvent.EventName);
            var AllRulesInAgent = eaCharater.GetAllAppraisalRules().ToList();



            if (PastEvents.Any())
            {
                List<AppraisalRuleDTO> OldappraisalRules = new();
                List<Eemotion> EmotionOfPastEvent = new();

                foreach (var RulesOfpastEvents in AllRulesInAgent)
                {
                    ///Aquí se calcula las emociones y su intensidad que los eventos pasados generarón en el agente,
                    ///para poder obtener un promedio de todos los eventos positivos y "centrar la atención" del agente en
                    ///esos eventos y minimizar el valor negativo del evento actual. 
                    foreach (var Event in PastEvents)
                    {
                        if (Event.GetNTerm(3).Equals(RulesOfpastEvents.EventMatchingTemplate.GetNTerm(3)))
                        {
                            OldappraisalRules.Add(RulesOfpastEvents);
                        }
                        else
                            break;
                        EmotionOfPastEvent = EmotionsDerivator(eaCharater, OldappraisalRules, Event);
                        ///Los eventos pasados a evaluar deberían ser unicamente positivos?, o podrían ser también negativos
                        ///para tener una prmedio de valores?
                    }
                }
                if (!EmotionOfPastEvent.Any() || EmotionOfPastEvent.Where(e => (float)e.EmotionType.Valence == 1) == null)
                {
                    Console.WriteLine("\n Strategy has not applied because :\n Doesn't exist related events : " + EmotionOfPastEvent.Any());
                    Console.WriteLine("\n Attention Deployment was applied : " + AppliedStrategy);
                    Console.WriteLine("---------------------------------------------------------------\n\n");
                    return AppliedStrategy;
                }


                Console.WriteLine(" \n In progress...  ");
                Console.WriteLine(" Evaluating past events...  ");

                var AuxEA = new EmotionalAppraisalAsset();
                var avg = EmotionOfPastEvent.Select(emo => emo.Intensity).Average(); ///avg de intensidad positiva de los eventos pasados
                foreach (var NegativeEmotion in NegativeEmotions)
                {
                    var AppraisalVariablesInEmotion = NegativeEmotion.AppraisalVariables;
                    var rules = new List<AppraisalRuleDTO>();

                    foreach (var old in AppraisalVariablesInEmotion)
                    {
                        var tanh = AppraisalFunction(old.Value.value, avg, 10, -(int)old.Value.valance);
                        var _target = string.Empty;
                        var _eventMatchingTemplate = string.Empty;

                        RulesOfEvent.ToList().ForEach(rule =>
                        {
                            var appraisalVariables = rule.AppraisalVariables.appraisalVariables;
                            _target = appraisalVariables.FirstOrDefault(v => v.Name == old.Key).Target.ToString();
                            _eventMatchingTemplate = rule.EventMatchingTemplate.ToString();
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
                    var potentialemotions = EmotionsDerivator(AuxEA, rules, EventToFAtiMA);
                    var Threshold = potentialemotions.LastOrDefault().Intensity <= ExpectedIntensity;

                    Console.WriteLine(" Must generate --> " + potentialemotions.FirstOrDefault().EmotionType.EmotionType + " : "
                                     + potentialemotions.LastOrDefault().Intensity
                                     + " \n                   Mood : " + potentialemotions.LastOrDefault().Mood);

                    if (Threshold)
                    {
                        foreach (var New in potentialemotions.FirstOrDefault().AppraisalVariables)
                        {
                            ///Aquí se están ya actualizando las variables del valoración
                            RulesOfEvent.ToList().ForEach(r => r.AppraisalVariables.appraisalVariables.FirstOrDefault(
                                v => v.Name == New.Key).Value = Name.BuildName(New.Value.value));
                        }
                        AppliedStrategy = true;
                    }
                    else
                    {
                        Console.WriteLine("\n Strategy has not applied because : Intensity threshold failed = " + !Threshold);
                        Console.WriteLine(" New possible intensity = " + potentialemotions.First().Intensity + " (User defined limit = " + ExpectedIntensity + ")");
                    }
                }

            }
            Console.WriteLine("\n Strategy has not applied because :\n Doesn't exist related events : " + PastEvents.Any());
            Console.WriteLine("\n Attention Deployment was applied : " + AppliedStrategy);
            Console.WriteLine("---------------------------------------------------------------\n\n");
            return AppliedStrategy;
        }

        // Cognitive Change
        private bool CognitiveChange(List<Eemotion> NegativeEmotions, List<AppraisalRuleDTO> RulesOfEvent)
        {
            Console.WriteLine("\n\n---------------------Cognitive Change------------------------");
            AppliedStrategy = false;

            var Mood = EmotionalStateCharacter.Mood;
            var AlternativeEvents = this.AlternativeEvents.Where(Event => Event.GetNTerm(4) == EventName);

            if (!AlternativeEvents.Any())
            {
                Console.WriteLine("\n Strategy has not applied because :\n Doesn't exist alternative events : " + !AlternativeEvents.Any());
                Console.WriteLine("\n Cognitive Change was applied : " + AppliedStrategy);
                Console.WriteLine("---------------------------------------------------------------\n\n");
                return AppliedStrategy;
            }
            var AllRulesInAgent = eaCharater.GetAllAppraisalRules().ToList();
            var EmotionsOfAlternativeEvt = new List<List<Eemotion>>();
            var IntensitySum = new List<float>();

            foreach (var alternativeEvent in AlternativeEvents)
            {
                var RulesOfAlternatveEvt = AllRulesInAgent.Where(
                r => r.EventMatchingTemplate.GetNTerm(3) == alternativeEvent.GetNTerm(3));
                /// Summary: Emoción generada por la reinterpretación del evento.
                ///     Se está calculando la emoción positiva a traves de las variables de valoración que el usuario ha elegido para la 
                ///     el evento que se tomará como una reinterpretación.
                ///     
                var EmotionsFelt = EmotionsDerivator(eaCharater, RulesOfAlternatveEvt, alternativeEvent);
                EmotionsOfAlternativeEvt.Add(EmotionsFelt);
                Console.WriteLine("\n '" + alternativeEvent.GetNTerm(3) + "'");
                EmotionsFelt.ForEach(e => Console.WriteLine($@"     Feeling emotion ---> {e.EmotionType.EmotionType} : {e.Intensity}"));
            }
            EmotionsOfAlternativeEvt.ForEach(ea =>
            { ea.Select(emo => emo.Intensity).ForEach(e => IntensitySum.Add(e)); });

            if (IntensitySum.Any())
            {
                var UnfitPersonality = (float)((Personality.Neuroticism + Personality.Agreeableness) / 2);
                var FitPersonality = (float)((Personality.Conscientiousness + Personality.Extraversion + Personality.Openness) / 3);
                var ReinterpretationAvg = IntensitySum.Average();

                var AuxEA = new EmotionalAppraisalAsset();
                foreach (var NegativeEmotion in NegativeEmotions)
                {
                    var AppraisalVariablesInEmotion = NegativeEmotion.AppraisalVariables;
                    var rules = new List<AppraisalRuleDTO>();
                    foreach (var old in AppraisalVariablesInEmotion)
                    {
                        var Valoration = (Mood + ReinterpretationAvg);
                        var UnfitTanh = AppraisalFunction(old.Value.value, UnfitPersonality, 100, -(int)old.Value.valance);
                        var FitTanh = AppraisalFunction(old.Value.value, FitPersonality, 100, (int)old.Value.valance);
                        var MoodIntensity = AppraisalFunction(old.Value.value, Valoration, 10, (int)old.Value.valance);
                        var tanh = (float)((UnfitTanh + MoodIntensity + FitTanh) / 3);

                        var _target = string.Empty;
                        var _eventMatchingTemplate = string.Empty;

                        RulesOfEvent.ToList().ForEach(rule =>
                        {
                            var appraisalVariables = rule.AppraisalVariables.appraisalVariables;
                            _target = appraisalVariables.FirstOrDefault(v => v.Name == old.Key).Target.ToString();
                            _eventMatchingTemplate = rule.EventMatchingTemplate.ToString();
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
                    var potentialemotions = EmotionsDerivator(AuxEA, rules, EventToFAtiMA); ///emociones potentiales

                    Console.WriteLine(" \n Must generate --> "
                        + potentialemotions.FirstOrDefault().EmotionType.EmotionType + " : "
                        + potentialemotions.LastOrDefault().Intensity
                        + " \n                   Mood : " + potentialemotions.LastOrDefault().Mood);

                    var Threshold = false;
                    if (potentialemotions.Where(e => (float)e.EmotionType.Valence == 1).Any())
                        Threshold = true;

                    Threshold = potentialemotions.LastOrDefault().Intensity <= ExpectedIntensity;
                    if (Threshold)
                    {
                        foreach (var New in potentialemotions.FirstOrDefault().AppraisalVariables)
                        {
                            ///Aquí se están ya actualizando las variables del valoración
                            RulesOfEvent.ToList().ForEach(r => r.AppraisalVariables.appraisalVariables.FirstOrDefault(
                                v => v.Name == New.Key).Value = Name.BuildName(New.Value.value));
                        } //TODO: Revisar que pasa si una emoción negativa, dentro de un conjunto de emociones de un mismo evento, falla el límite. 
                        AppliedStrategy = true;///Si una emoción negativa falla??, la cuestión es que se está considerando que un evento soló 
                                               ///puede generar un solo tipo de emoción, sin embargo FAtiMA permite generar varias emociones en un mismo evento, por
                                               ///eso fue necesario reajustar todo el código. Por ahora ya solo faltaría revisar eso de la multiple valoración de un
                                               ///mismo evento.
                    }
                    else
                    {
                        Console.WriteLine("\n Strategy has not applied because : Intensity threshold failed = " + !Threshold);
                        Console.WriteLine(" New possible intensity = " + potentialemotions.FirstOrDefault().Intensity + " (User defined limit = " + ExpectedIntensity + ")");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n Strategy has not applied because :\n Doesn't exist alternative events : " + !IntensitySum.Any());
            }
            Console.WriteLine("\n Cognitive Change was applied : " + AppliedStrategy);
            Console.WriteLine("---------------------------------------------------------------\n\n");
            return AppliedStrategy;
        }

        //Response Modulation
        private bool ResponseModulation()
        {
            Console.WriteLine("\n\n---------------------Response Modulation------------------------");
            AppliedStrategy = false;

            Console.WriteLine($"\nEvent name: {EventName}                              Target: {Target}");
            Console.WriteLine(" \n In progress...  ");
            Console.WriteLine(" Evaluating emotion intensity...  \n");

            var ActiveEmotion = EmotionalStateCharacter.GetAllEmotions().LastOrDefault();
            Console.WriteLine(" \n  Mood before Emotion Regulation  = " + EmotionalStateCharacter.Mood);
            Console.WriteLine("  Emotions before Emotion Regulation : \n  "
                               + string.Concat(ActiveEmotion.EmotionType + " : " + ActiveEmotion.Intensity + " "));

            /// Summary 
            ///     Por como quedó la nueva tabla de las personalidades, en tanto en cuanto 
            ///     exista una personalidad dominante, no se aplicará esta estrategia.
            var _personalitiesValues = Personality.BigFiveTypeReal;
            var allTraits = (float)_personalitiesValues.Average();
            var PersonalityBasedEmotion = AppraisalFunction(allTraits, ActiveEmotion.Intensity, 5, 1) + allTraits;
            var newIntensity = (float)Math.Round(AppraisalFunction(ActiveEmotion.Intensity, PersonalityBasedEmotion, 100, -1));

            var Threshold = newIntensity <= ExpectedIntensity;

            if (Threshold)
            {
                var MoodDueToEvent = (float)ActiveEmotion.Valence * (ActiveEmotion.Intensity * FAtiMAConfiguration.MoodInfluenceOnEmotionFactor);
                var valueMoodDueToEvent = MoodDueToEvent < -10 ? -10 : (MoodDueToEvent > 10 ? 10 : MoodDueToEvent);
                var CurrentMoodDueToEvent = EmotionalStateCharacter.Mood;
                var MoodWithoutEventValue = CurrentMoodDueToEvent - MoodDueToEvent;

                //To create and update the emotion
                var NewEmotion = new EmotionalAppraisal.DTOs.EmotionDTO
                {
                    CauseEventId = ActiveEmotion.CauseId,
                    Type = ActiveEmotion.EmotionType,
                    Intensity = newIntensity,
                    CauseEventName = EventName.ToString(),
                    Target = Target.ToString(),
                };

                var AuxConcreteEmotion = new ConcreteEmotionalState();
                EmotionalStateCharacter.RemoveEmotion(ActiveEmotion, amCharacter); ///Remove the emotion from agent
                AuxConcreteEmotion.AddActiveEmotion(NewEmotion, amCharacter); ///Add new emotion in agent
                var NewActiveEmotion = AuxConcreteEmotion.GetAllEmotions().LastOrDefault();
                var emoDisp = eaCharater.GetEmotionDisposition(ActiveEmotion.EmotionType); ///Making new emotion
                emoDisp.Threshold = 1;
                var tick = amCharacter.Tick;
                EmotionalStateCharacter.Mood = 0;
                var NewEmotionalIntensity = EmotionalStateCharacter.AddEmotion(///add new intensity emotion in the agent
                NewActiveEmotion, amCharacter, emoDisp, tick);
                var e = EmotionalStateCharacter.GetAllEmotions().LastOrDefault().Intensity;
                if (NewEmotionalIntensity != null)
                    Console.WriteLine("\n Calculated Intensity = " + NewEmotionalIntensity.Intensity + "  New Intesity = " + e);

                //Update the Mood
                var NewEmoValence = EmotionalStateCharacter.GetAllEmotions().Select(e => (float)e.Valence).LastOrDefault();
                var NewEmoIntencity = EmotionalStateCharacter.GetAllEmotions().Select(e => e.Intensity).LastOrDefault();
                var moodDouToNewIntensity = NewEmoValence * (NewEmoIntencity * FAtiMAConfiguration.MoodInfluenceOnEmotionFactor);
                var MoodDouToNewIntensity = moodDouToNewIntensity < -10 ? -10 : (moodDouToNewIntensity > 10 ? 10 : moodDouToNewIntensity);
                var NewMood = MoodWithoutEventValue + MoodDouToNewIntensity;
                EmotionalStateCharacter.Mood = NewMood;

                Console.WriteLine(" \n  New Mood = " + EmotionalStateCharacter.Mood);
                Console.WriteLine("  New Emotion: \n  "
                + string.Concat(EmotionalStateCharacter.GetAllEmotions().Select(
                e => e.EmotionType + ": " + e.Intensity + " ")));
                AppliedStrategy = true;
            }
            else
            {
                Console.WriteLine("\n Strategy has not applied because : Intensity threshold failed = " + !Threshold);
                Console.WriteLine(" New possible intensity = " + Math.Abs(newIntensity) + " (User defined limit = " + this.ExpectedIntensity + ")");
            }
            Console.WriteLine("\n Response Modulation was applied: " + AppliedStrategy);
            Console.WriteLine("---------------------------------------------------------------\n\n");
            return AppliedStrategy;
        }

    #region Emotion Regulation Resources

        public InputDataFrame AntecedentFocused(List<string> strategies, Name Event)
        {
            Target = Event.GetNTerm(4);
            EventName = Event.GetNTerm(3);
            IDevent++;
            bool StrategySuccess = false;
            string strategyName = string.Empty;
            (string, string) result;

            var WellformedEvent = EventValidation(Event);///Reconstruction of the received event.
            EventToFAtiMA = WellformedEvent.Event;
            var RulesOfEvent = eaCharater.GetAllAppraisalRules().Where(///para calcular las emociones potentiales en el agente.
                r => r.EventMatchingTemplate.GetNTerm(3) == EventName).ToList();

            Console.WriteLine("Event information : " + Event);
            var CalucalteEmotion = EmotionsDerivator(eaCharater, RulesOfEvent, EventToFAtiMA); ///emociones potentiales
            var MoodWithoutER = ER_Mood + EmotionalStateCharacter.Mood;
            Console.WriteLine("\n Mood if won't use ER : " + MoodWithoutER);
            OutputData.NormalMood.Add(MoodWithoutER);
            foreach (var negative in CalucalteEmotion)
            {
                Console.WriteLine(" Emotions : " + negative.EmotionType.EmotionType + " : " + negative.Intensity);
                OutputData.NormalEmotions.Add(negative.Intensity);
                
            }

            Console.WriteLine("\n The agent personalities are:\n ");
            Personality.Personalities.ForEach(p => Console.WriteLine("   " + p));
            Console.WriteLine($"\n The dominant personality is: {Personality.DominantPersonality}");
            Console.WriteLine("\n The strategies that could be applied are : \n ");
            Personality.StrategiesToApply.ForEach(s => Console.WriteLine("  " + s.Key + " ---> " + s.Value));

            var NegativeEmotions = CalucalteEmotion.Where(e => e.EmotionType.Valence.ToString() == "Negative").ToList();
            if (!NegativeEmotions.Any())
            {
                result = (string.Concat(IDevent + ": " + EventToFAtiMA.GetNTerm(3)), "None ");
                OutputData.Results.Add(result);
                OutputData.StrategySuccessful = true;
                return OutputData;
            }

            foreach (var strategy in strategies)
            {
                strategyName = strategy;

                if (strategy == SITUATION_SELECTION)
                { StrategySuccess = SituationSelection(EventToFAtiMA, WellformedEvent.IsAvoided); if (StrategySuccess) break; continue; }
                else
                if (strategy == SITUATION_MODIFICATION && !StrategySuccess)
                { StrategySuccess = SituationModification(NegativeEmotions, RulesOfEvent); if (StrategySuccess) break; continue; }
                else
                if (strategy == ATTENTION_DEPLOYMENT && !StrategySuccess)
                { StrategySuccess = AttentionDeployment(NegativeEmotions, RulesOfEvent); if (StrategySuccess) break; continue; }
                else
                if (strategy == COGNITIVE_CHANGE && !StrategySuccess)
                { StrategySuccess = CognitiveChange(NegativeEmotions, RulesOfEvent); if (StrategySuccess) break; continue; }
            }
            if (StrategySuccess)
            { result = (string.Concat(IDevent + ": " + EventToFAtiMA.GetNTerm(3)), strategyName); OutputData.Results.Add(result); }
            else if (strategyName != RESPONSE_MODULATION)
            { result = (string.Concat(IDevent + ": " + EventToFAtiMA.GetNTerm(3)), "None "); OutputData.Results.Add(result); }

            OutputData.StrategySuccessful = StrategySuccess;

            return OutputData;
        }
        public InputDataFrame ResponseFocused(List<string> strategies)
        {
            (string, string) result;
            bool StrategySuccess;

            if (strategies.Contains(RESPONSE_MODULATION))
            {
                StrategySuccess = ResponseModulation();
            }
            else
                return OutputData;

            if (StrategySuccess)
            { result = ((string.Concat(IDevent + ": " + EventToFAtiMA.GetNTerm(3)), RESPONSE_MODULATION)); }
            else
            {
                result = (string.Concat(IDevent + ": " + EventToFAtiMA.GetNTerm(3)), "None ");
                Console.WriteLine("\n\n---------------------Response Modulation------------------------");
                Console.WriteLine("\n Response Modulation was applied: " + AppliedStrategy);
                Console.WriteLine(" The agent's personality cannot apply Response Modulation");
                Console.WriteLine("--------------------------------------------------------------------\n\n");
            }
            OutputData.Results.Add(result);
            OutputData.StrategySuccessful = StrategySuccess;

            return OutputData;
        }
        private List<Eemotion> EmotionsDerivator(EmotionalAppraisalAsset character, IEnumerable<AppraisalRuleDTO> appraisalRules, Name EventName)
        {
            List<Eemotion> EmotionsOfEvent = new();
            List<KeyValuePair<string, (float value, EmotionValence valance)>> keyValuePairs = new();
            Eemotion BaseEmotion = new() { AppraisalVariables = keyValuePairs };

            var FAtiMAconfigs = FAtiMAConfiguration;
            var EmotionIntensity = new OCCAffectDerivationComponent();
            var ERframe = new EmotionRegulationFrame();
            var Intensity = 0f;
            var value = 0f;

            ERframe.EmotionRegulationGetAppRules(ERframe, appraisalRules);
            var BASE_EVENT = new EventByRegulation(0, EventName, 0);
            ERframe.AppraisedEvent = BASE_EVENT;
            var emotions = EmotionIntensity.AffectDerivation(character, null, ERframe);

            if (EmotionalStateCharacter.Mood != 0) { ER_Mood = EmotionalStateCharacter.Mood; } else { ER_Mood = 0; }

            foreach (var emotion in emotions)
            {
                var CurrentAppraisals = emotion.AppraisalVariables.ToList();
                List<KeyValuePair<string, (float value, EmotionValence valance)>> appraisalvariables = new();
                CurrentAppraisals.ForEach(c =>
                    appraisalvariables.Add(ERframe.RegulationAppraisalVariables.FirstOrDefault(app => app.Key == c)));

                var (_Intensity, Mood) = Determinepotential(emotion);
                ER_Mood = Mood;

                BaseEmotion.Mood = ER_Mood;
                BaseEmotion.EmotionType = emotion;
                BaseEmotion.Intensity = _Intensity;
                BaseEmotion.AppraisalVariables = appraisalvariables;

                EmotionsOfEvent.Add(BaseEmotion);
            }
            
            
            (float Intensity, float Mood) Determinepotential(IEmotion emotion)
            {
                float potential = emotion.Potential;
                float scale = (float)emotion.Valence;
                potential += scale * (ER_Mood * FAtiMAconfigs.MoodInfluenceOnEmotionFactor);

                
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
                    return (0f, ER_Mood);
                }
            }
            float UpdateMood(float scale, float potential)
            {
                value = Intensity + scale * (potential * FAtiMAconfigs.EmotionInfluenceOnMoodFactor);
                Intensity = value;

                return Intensity;
            }

            return EmotionsOfEvent;
        }
        public static (Name Event, bool IsAvoided) EventValidation(Name Event)
        {
            var IsAvoided = false;
            if (Event.NumberOfTerms > 5)
            {
                var ListEvent = Event.GetLiterals().ToList();
                var EventValues = string.Join(
                    "", ListEvent.Last().ToString().Split('[', ']')).Split("-").LastOrDefault();

                IsAvoided = bool.Parse(EventValues.ToLower());
                var NameLenght = ListEvent.Count - 1;

                for (int j = NameLenght; j <= ListEvent.Count; j++)
                {
                    ListEvent.RemoveAt(5);
                }
                Event = Name.BuildName(ListEvent);
            }

            return (Event, IsAvoided);
        }
        private static float AppraisalFunction(float valoration, float evaluation, int lim, int valance)
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
    #endregion

    #region FAtiMA Utilities
    partial class EmotionRegulationFrame : IAppraisalFrame
    {
        private Dictionary<string, float> appraisalVariables = new();
        private Dictionary<string, (float value, EmotionValence valance)> ER_appraisalVariables = new();

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
    partial class EventByRegulation : IBaseEvent
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