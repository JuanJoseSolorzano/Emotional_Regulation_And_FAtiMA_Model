using System;
using System.Linq;
using EmotionalAppraisal;
using EmotionalDecisionMaking;
using AutobiographicMemory;
using WellFormedNames;
using KnowledgeBase;
using GAIPS.Rage;
using System.Collections.Generic;
using ActionLibrary.DTOs;
using RolePlayCharacter;
using EmotionalAppraisal.OCCModel;
using SpreadsheetLight;
using EmotionalAppraisal.DTOs;
using System.IO;
using IntegratedAuthoringTool;
using DocumentFormat.OpenXml.Bibliography;

//
namespace Emotion_Regulation_V01
{
    class MainClass
    {
        static RolePlayCharacterAsset auxAgent = new RolePlayCharacterAsset
        {
            CharacterName = (Name)"Pedro",
            m_emotionalAppraisalAsset = new EmotionalAppraisalAsset()
        };
        static List<double> NormalEmotionsForScottPlot = new List<double>();
        static List<double> NormalMoodForScottPlot = new List<double>();
        static string path;
        public struct AgentSimulator
        {
            public RolePlayCharacterAsset RPC;
            public KB KB;
            public AM AM;
            public EmotionalAppraisalAsset EA;
            public ConcreteEmotionalState CE;
            public PersonalityTraits Personality;
            public EmotionRegulationAsset ER;
            public EmotionalDecisionMakingAsset EDM;
        }
        public struct CompusedAppraisal
        {
            public string OCC_Variable;
            public float Value;
            public string Target;
        }
        struct DataFrame
        {
            public System.Data.DataTable dataTable;
            public SLDocument SLdocument;
            public string pathFile;
        }


        #region Simulation Resources
        static (string relatedAction, string eventName) SplitActionName(string actionEvent)
        {
            var SpecialCharacter = actionEvent.Split("|");
            var RelatedAction = SpecialCharacter[0].Trim();
            var RelatedEvent = SpecialCharacter[1].Trim();
            (string, string) EventsActions = (RelatedAction, RelatedEvent);
            return EventsActions;
        }
        static AgentSimulator BuildRPCharacter(string name, string body)
        {
            EmotionalAppraisalAsset ea_Character = EmotionalAppraisalAsset.CreateInstance(new AssetStorage());
            var storage = new AssetStorage();

            var character = new AgentSimulator
            {
                KB = new KB((Name)name),
                AM = new AM() { Tick = 0, },
                CE = new ConcreteEmotionalState(),
                EA = ea_Character,
                EDM = EmotionalDecisionMakingAsset.CreateInstance(storage)
            };

            character.RPC = new RolePlayCharacterAsset
            {
                BodyName = body,
                VoiceName = body,
                CharacterName = (Name)name,
                m_kb = character.KB,
            };
            character.RPC.LoadAssociatedAssets(new AssetStorage());

            return character;
        }
        static DataFrame CreateDataframe(string agentName, PersonalityTraits personality, bool haveER)
        {
            DataFrame DF = new();

            var origen = @"..\..\..\Results\";

            var DominantPersonality = personality.DominantPersonality;
            var Dominant = string.Concat("_" + DominantPersonality);

            if (!haveER) { DominantPersonality = string.Empty; }

            if (string.IsNullOrEmpty(DominantPersonality))
            {
                path = origen + agentName + "_NotPersonalityDominant" + ".xlsx";
            }
            else
                path = origen + agentName + Dominant + ".xlsx";

            //Data frama
            string pathFile = AppDomain.CurrentDomain.DynamicDirectory + path;
            SLDocument oSLDocument = new();
            System.Data.DataTable df = new();
            //columnas
            df.Columns.Add("MOOD     ", typeof(float));
            df.Columns.Add("EMOTION  ", typeof(string));
            df.Columns.Add("INTENSITY", typeof(float));
            df.Columns.Add("   EVENT    ", typeof(string));
            df.Columns.Add(" APPLIED STRATEGY    ", typeof(string));
            df.Columns.Add(" PERSONALITY TRAITS ", typeof(string));

            DF.dataTable = df;
            DF.SLdocument = oSLDocument;
            DF.pathFile = pathFile;

            return DF;
        }
        static void UpdateAppraisalRulesComposed(EmotionalAppraisalAsset EA, List<CompusedAppraisal> variables, string eventMatch)
        {
            var _AppraisalVariableDTO = new List<EmotionalAppraisal.DTOs.AppraisalVariableDTO>();
            foreach (var Appraisal in variables)
            {
                _AppraisalVariableDTO.Add(new EmotionalAppraisal.DTOs.AppraisalVariableDTO()
                {
                    Name = Appraisal.OCC_Variable,
                    Value = Name.BuildName(Appraisal.Value),
                    Target = Name.BuildName(Appraisal.Target)
                });
            }
            var rule = new EmotionalAppraisal.DTOs.AppraisalRuleDTO()
            {

                EventMatchingTemplate = Name.BuildName("Event(Action-End, *," + eventMatch + ", *)"),
                AppraisalVariables = new AppraisalVariables(_AppraisalVariableDTO),
            };
            EA.AddOrUpdateAppraisalRule(rule);
        }
        static void UpdateAppraisalRules(EmotionalAppraisalAsset EA, string variable, float value, string target, Name eventMatch)
        {
            var appraisalVariableDTO = new List<AppraisalVariableDTO>()
            {
                new AppraisalVariableDTO()
                {
                    Name = variable,
                    Value = Name.BuildName(value),
                    Target = Name.BuildName(target)
                }
            };
            var rule = new EmotionalAppraisal.DTOs.AppraisalRuleDTO()
            {
                EventMatchingTemplate = Name.BuildName("Event(Action-End, *," + eventMatch.ToString() + ", *)"),
                AppraisalVariables = new AppraisalVariables(appraisalVariableDTO),
            };
            EA.AddOrUpdateAppraisalRule(rule);
        }
        static Dictionary<string, List<Name>> UpdateEvents()
        {
            /// Summary:
            ///+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            ///+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            ///
            /// “Pedro es un oficinista que trabaja para una empresa desde hace algunos años. A lo largo del día Sam experimenta
            /// diferentes emociones a causa de los eventos que suceden en su alrededor. Un día normal para Pedro inicia al 
            /// llegar a su trabajo”:
            /// 
            /// Evento 1: Pedro es notificado para hablar con su jefe (“Talk-To-Boss”). 
            ///     Al entrar en su oficina, Pedro es notificado por otros compañeros del trabajo que su jefe solicita su 
            ///     presencia antes de que se termine el día laboral. Esta noticia desencadena en Pedro una emoción de 
            ///     aflicción, ya que no sabe con certeza el motivo porque su jefe solicita hablar con él. 
            ///     
            /// Evento 2: Pedro saluda a María(“Hello”). 
            ///     El tiempo transcurre mientras Pedro realiza diferentes tareas de rutina. De pronto, llega a su oficina
            ///     María, una compañera de trabajo que le gusta mucho.Al verla y saludarla Pedro experimenta la emoción de 
            ///     alegría.
            ///     
            /// Evento 3: Pedro conversa con María(“Conversation”).
            ///     María decide charlar por un momento con Pedro y contarle como va su día, esto desencadena una emoción de 
            ///     orgullo en Pedro, ya que le ha tomado algún tiempo ganarse la aceptación de María.
            ///     
            /// Evento 4: María abraza a Pedro(“Hug”). 
            ///     Al terminar la charla, María le da un abrazo a Pedro como una señal de confianza y aprobación.Este gesto
            ///     provoca que Pedro experimente una emoción de amor por María.
            ///     
            /// Evento 5: Pedro Discute con compañeros de la oficina(“Discussion”).
            ///     Después de haber charlado con María, Pedro se dispone a tomar un descanso en su hora de almuerzo. Durante 
            ///     su descanso Pedro se encuentra con otros compañeros del trabajo con quienes discute a causa de los partidos
            ///     de fútbol de sus equipos favoritos.Esta discusión provoca en Pedro una emoción negativa de enfado.
            ///     
            /// Evento 6: Pedro es felicitado por su amigo(“Congrat”). 
            ///     Terminada su hora de almuerzo, Pedro regresa a su oficina para continuar con su trabajo. Mientras se dirigía
            ///     a su oficina Pedro se encuentra con su mejor amigo, Javier, quien lo felicita por haber logrado comprar a
            ///     tiempo las entradas a un partido de fútbol. La felicitación de su amigo Javier provoca en Pedro una emoción
            ///     de orgullo.
            ///     
            /// Evento 7: María le da la noticia sobre su renuncia(“Bye”). 
            ///     Un poco antes de terminarse la jornada laboral, Pedro vuelve a encontrarse con María, quien ahora le da la
            ///     mala noticia de que ha renunciado al trabajo por asuntos personales, y que tendrá que mudarse a otra ciudad.
            ///     Pedro siente aflicción al darse cuenta de que ya no volverá a verla en la oficina.
            ///     
            /// Evento 8: Pedro es despedido de su trabajo(“Fired”).
            ///     Se ha llegado la hora de salir del trabajo para Pedro, con lo cual, tiene que atender la petición de su jefe
            ///     e ir a hablar con él. Al llegar a su oficina, el jefe le da la mala noticia de que desafortunadamente ha 
            ///     sido despedido debido a que la empresa está pasando problemas financieros y han tenido que realizar un 
            ///     recorte de personal.Como es de esperarse este evento genera en el agente Pedro una emoción negativa de 
            ///     aflicción.
            ///     
            /// Evento 9: Pedro sufre un percance automovilístico(“Crash”).
            ///     Al salir de su trabajo, Pedro se dirige a su casa después de haber pasado por una jornada laboral muy
            ///     intensa.Mientras conduce a casa, recuerda que al salir por la mañana de su hogar se olvidó de asegurar la 
            ///     puerta principal. Por lo que decide conducir más aprisa y con menos precaución.De pronto, Pedro no ha 
            ///     prestado la atención suficiente y choca con otro automóvil que estaba haciendo stop en una señal de tránsito.
            ///     Afortunadamente, el accidente solo provocó algunos daños materiales.Desde luego nada, nada oportuno para la 
            ///     situación actual de Pedro, lo que desencadena una emoción negativa de enfado en el agente.
            ///     
            /// Evento 10: Pedro recupera su trabajo(Rehired). Después del choque, Pedro continúa el viaje a casa, durante el
            ///     trayecto recibe una llamada de su jefe, quien le da la buena noticia que se ha cometido un error y no es él
            ///     quien está despedido, sino todo lo contrario, la empresa lo ha promovido de puesto.Tal noticia desencadena en
            ///     Pedro una emoción de gratificación y alegría.
            ///     
            /// Evento 11: Robo en casa de Pedro(Robbery).
            ///     Al llegar a su casa, Pedro se dio cuenta de que efectivamente no había asegurado la puerta principal. Cuando
            ///     entró a su casa, se encontró con que todos sus objetos de valor habían sido robados. Debido a esto, Pedro 
            ///     experimentó una emoción de aflicción. 
            /// 
            ///+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            ///+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-++-+-+-+-+      

            ///Past events (Attetional deployment) T->T
            var HadParty = Name.BuildName("Event(Action-End, Pedro,   Had-Party, Workmates)");     //Discussion
            var HadTravels = Name.BuildName("Event(Action-End, Pedro, Had-Travels, Workmates)"); //Discussion
            var OtherJobs = Name.BuildName("Event(Action-End, Pedro, Other-Jobs, Works)");       //Fired
            var BetterBye = Name.BuildName("Event(Action-End, Pedro, A-Better-Bye, Sarah)");       //Bye
            var OtherCats = Name.BuildName("Event(Action-End, Pedro, Could-Be-Worst, House)");         //Robbery
            List<Name> PastEvents = new() { HadParty, HadTravels, BetterBye, OtherJobs, OtherCats };

            ///Emotion regulation events
            var TalktoBoss = Name.BuildName("Event(Action-End, Pedro, Talk-To-Boss, Boss, [false])");
            var Hello = Name.BuildName("Event(Action-End, Pedro, Hello, Sarah)");
            var Conversation = Name.BuildName("Event(Action-End, Pedro, Conversation, Sarah)");
            var Hug = Name.BuildName("Event(Action-End, Pedro, Hug, Sarah)");
            var Discussion = Name.BuildName("Event(Action-End, Pedro, Discussion, Others, [true])");
            var Congrat = Name.BuildName("Event(Action-End, Pedro, Congrat, Sarah)");
            var Bye = Name.BuildName("Event(Action-End, Pedro, Bye, Sarah, [false])");
            var Fired = Name.BuildName("Event(Action-End, Pedro, Fired, Boss, [false])");
            var Crash = Name.BuildName("Event(Action-End, Pedro, Crash, Car, [false])");
            var Rehired = Name.BuildName("Event(Action-End, Pedro, Rehired, Pedro)");
            var Robbery = Name.BuildName("Event(Action-End, Pedro, Robbery, House, [false])");
            List<Name> ERevents = new()
            {
                TalktoBoss,
                Hello,
                Conversation,
                Hug,
                Discussion,
                Congrat,
                Bye,
                Fired,
                Crash,
                Rehired,
                Robbery,
            };

            ///Alternative events (cognitive change)
            var Event1 = Name.BuildName("Event(Action-End, Pedro, Dont-HaveTo-Work, Fired)"); //Fired
            var Event2 = Name.BuildName("Event(Action-End, Pedro, Meeting-New-People, Fired)");  //Fired
            var Event3 = Name.BuildName("Event(Action-End, Pedro, Getting-New-Job, Fired)");      //Fired
            var Event4 = Name.BuildName("Event(Action-End, Pedro, Increase-Salary, Talk-To-Boss)");
            var Event5 = Name.BuildName("Event(Action-End, Pedro, Buying-New-Car, Crash)");
            var Event6 = Name.BuildName("Event(Action-End, Pedro, Could-Be-Worst, Robbery)");
            List<Name> AlternativeEvents = new()
            {
                Event1,
                Event2,
                Event3,
                Event4,
                Event5,
                Event6
            };

            Dictionary<string, List<Name>> Events = new()
            {
                { "PastEvents", PastEvents },
                { "ERevents", ERevents },
                { "AlternativeEvents", AlternativeEvents }
            };

            return Events;
        }
        static Dictionary<string, string> CreateActions(EmotionalDecisionMakingAsset eDMcharacter)
        {
            Dictionary<string, string> ActionsToEvents = new();

            //Action for the event: Talk 
            var ActionEventEnter = "Joke | Talk-To-Boss";
            var TeventActionEnter = SplitActionName(ActionEventEnter);
            var ER_EnterAction = new ActionRuleDTO
            {
                Action = Name.BuildName(TeventActionEnter.relatedAction),
                Priority = Name.BuildName("1"),
                Target = (Name)"Office",
            };
            var idER_Enter = eDMcharacter.AddActionRule(ER_EnterAction);
            eDMcharacter.AddRuleCondition(idER_Enter, "Current(Location) = Office");
            eDMcharacter.Save();
            ActionsToEvents.Add(TeventActionEnter.relatedAction, TeventActionEnter.eventName);

            //Action for the event: Talk 
            var ActionEventEnter2 = "Wait | -";
            var TeventActionEnter2 = SplitActionName(ActionEventEnter2);
            var ER_EnterAction2 = new ActionRuleDTO
            {
                Action = Name.BuildName(TeventActionEnter2.relatedAction),
                Priority = Name.BuildName("5"),
                Target = (Name)"Office",
            };
            var idER_Enter2 = eDMcharacter.AddActionRule(ER_EnterAction2);
            eDMcharacter.AddRuleCondition(idER_Enter2, "Current(Location) = Office");
            eDMcharacter.Save();
            ActionsToEvents.Add(TeventActionEnter2.relatedAction, TeventActionEnter2.eventName);

            //Action for the event: Discussion 
            var ActionEventDiscussion = "ShutUp | -";
            var TeventActionDiscussion = SplitActionName(ActionEventDiscussion);
            var ER_Discussion = new ActionRuleDTO
            {
                Action = Name.BuildName(TeventActionDiscussion.relatedAction),
                Priority = Name.BuildName("5"),
                Target = (Name)"Office",
            };
            var idER_Discussion = eDMcharacter.AddActionRule(ER_Discussion);
            eDMcharacter.AddRuleCondition(idER_Discussion, "Current(Location) = Office");
            eDMcharacter.Save();
            ActionsToEvents.Add(TeventActionDiscussion.relatedAction, TeventActionDiscussion.eventName);

            //Action for the event: Bye
            var ActionNameBye = "ToHug | Bye";
            var DiccEventActionBye = SplitActionName(ActionNameBye);
            var ER_ByeAction = new ActionRuleDTO
            {
                Action = Name.BuildName(DiccEventActionBye.relatedAction),
                Priority = Name.BuildName("1"),
                Target = (Name)"Sarah",
            };
            var idER_Bye = eDMcharacter.AddActionRule(ER_ByeAction);
            eDMcharacter.AddRuleCondition(idER_Bye, "Like(Sarah) = True");
            eDMcharacter.Save();
            ActionsToEvents.Add(DiccEventActionBye.relatedAction, DiccEventActionBye.eventName);

            //Action for event Fired
            var ActionNameFired = "Fired | Cry";
            var DiccEventActionFired = SplitActionName(ActionNameFired);
            var ER_FiredAction = new ActionRuleDTO
            {
                Action = Name.BuildName(DiccEventActionFired.relatedAction),
                Priority = Name.BuildName("1"),
                Target = (Name)"SELF",
            };
            var idER_Fired = eDMcharacter.AddActionRule(ER_FiredAction);
            eDMcharacter.AddRuleCondition(idER_Fired, "Current(Location) = Office");
            eDMcharacter.Save();
            ActionsToEvents.Add(DiccEventActionFired.relatedAction, DiccEventActionFired.eventName);
            var ActionNameRich = "BuyAll|BecomeRich";
            var DiccEventActionRich = SplitActionName(ActionNameRich);
            var ER_RichAction = new ActionRuleDTO
            {
                Action = Name.BuildName(DiccEventActionRich.relatedAction),
                Priority = Name.BuildName("1"),
                Target = (Name)"SELF",
            };
            var idER_Rich = eDMcharacter.AddActionRule(ER_RichAction);
            eDMcharacter.AddRuleCondition(idER_Rich, "Current(Location) = Office");
            eDMcharacter.Save();
            ActionsToEvents.Add(DiccEventActionRich.relatedAction, DiccEventActionRich.eventName);

            return ActionsToEvents;
        }
        static (float O, float C, float E, float A, float N) RandomPersonality()
        {
            var rand = new Random();
            var o = (float)rand.NextDouble() * 66f;
            var c = (float)rand.NextDouble() * 66f;
            var e = (float)rand.NextDouble() * 66f;
            var a = (float)rand.NextDouble() * 66f;
            var n = (float)rand.NextDouble() * 66f;

            return (o, c, e, a, n);
        }
        #endregion

        static void Simulations(AgentSimulator character, List<Name> eventEvaluations, bool IsPastEvent, bool HaveER)
        {
            List<double> MoodPlot = new();
            List<double> EmotionPlot = new();
            List<string> EmotionNamePlot = new();

            var AgentName = character.KB.Perspective.ToString();
            var dataFrame = CreateDataframe(AgentName, character.Personality, HaveER);
            EmotionRegulationAsset.InputDataFrame StrategiesResults = new();
            var CalucalteEmotion = new EmotionRegulationAsset();
            List<string> strategyName = new();

            Console.WriteLine(" \n                 " + AgentName.ToUpper() + "'s PERSPECTIVE \n");
            foreach (var evt in eventEvaluations)
            {
                var Event = (Name)"null";

                if (HaveER)
                {
                    strategyName = character.Personality.StrategiesToApply.Where(v => v.Value == "Strongly").Select(s => s.Key).ToList();
                    StrategiesResults = character.ER.AntecedentFocused(strategyName, evt);
                    Event = character.ER.EventToFAtiMA;
                }
                else
                    Event = EmotionRegulationAsset.EventValidation(evt).Event; /// Para facilitar la simulación, siempre se reconstruye el evento.

                character.EA.AppraiseEvents(new[] { Event }, character.CE, character.AM, character.KB, null);
                Console.WriteLine(" \n Events occured so far: "
                                            + string.Concat(character.AM.RecallAllEvents().Select(e => "\n Id: "
                                            + e.Id + " Event: " + e.EventName.ToString())));

                if (HaveER && !StrategiesResults.StrategySuccessful)
                {
                    StrategiesResults = character.ER.ResponseFocused(strategyName);
                }

                auxAgent.Perceive(EmotionRegulationAsset.EventValidation(evt).Event);
                auxAgent.Update();
                var auxOrdenedEmotions = auxAgent.GetAllActiveEmotions().OrderBy(x2 => x2.CauseEventId);



                character.AM.Tick++;
                character.CE.Decay(character.AM.Tick);
                var emotionalState = character.CE.GetAllEmotions().OrderBy(x1 => x1.CauseId);
                Console.WriteLine(" \n  Mood on tick '" + character.AM.Tick + "': " + character.CE.Mood);
                Console.WriteLine("  Active Emotions \n  "
                        + string.Concat(emotionalState.Select(e => e.EmotionType + ": " + e.Intensity + " ")));

                Console.WriteLine("\n  Auxiliar agent Mood: " + auxAgent.Mood);
                Console.WriteLine("\n  Auxiliar agent feels \n  "
                        + string.Concat(auxOrdenedEmotions.Select(e1 => e1.Type + ": " + e1.Intensity + " ")));

                character.EA.Save();
                var OredendEmotions = character.CE.GetAllEmotions().OrderBy(x => x.CauseId);

                ///-----------------------------------DATASET--------------------------------///
                if (!IsPastEvent)
                {

                    NormalEmotionsForScottPlot.Add(auxOrdenedEmotions.LastOrDefault().Intensity);
                    NormalMoodForScottPlot.Add(auxAgent.Mood);
                    Console.WriteLine("\n-------------------------- RESUMEN ----------------------------\n ");

                    if (HaveER)
                        StrategiesResults.Results.ForEach(r => Console.WriteLine(r));
                    Console.WriteLine("\n---------------------------------------------------------------\n ");
                    var MOOD = character.CE.Mood;
                    var EMOTION = ""; var INTESITY = 0f; var STRATEGY = ""; var EVENT = "";
                    if (HaveER && StrategiesResults.Results.LastOrDefault().Strategy == EmotionRegulationAsset.SITUATION_SELECTION)
                    {
                        EMOTION = "None";
                        INTESITY = 0.0f;
                    }
                    else
                    {
                        EMOTION = OredendEmotions.Select(e => e.EmotionType).LastOrDefault();
                        INTESITY = OredendEmotions.Select(e => e.Intensity).LastOrDefault();
                    }
                    if (HaveER)
                    {
                        STRATEGY = StrategiesResults.Results.Select(s => s.Strategy).LastOrDefault();
                        EVENT = StrategiesResults.Results.Select(e => e.Event).LastOrDefault();
                    }
                    else
                        EVENT = Event.GetNTerm(3).ToString();

                    dataFrame.dataTable.Rows.Add(MOOD, EMOTION, INTESITY, EVENT, STRATEGY);

                    ///Plots
                    var NameParts = STRATEGY.Split(" ").ToArray();
                    MoodPlot.Add(MOOD);
                    EmotionPlot.Add(INTESITY);
                    EmotionNamePlot.Add(EMOTION + "\n" + EVENT + "\n" + NameParts[0] + "\n" + NameParts[1]);
                }
                ///--------------------------------------------------------------------------///
                else
                {
                    while (true)
                    {
                        character.AM.Tick++;
                        auxAgent.Update();
                        character.CE.Decay(character.AM.Tick);
                        var Intensity = character.CE.GetAllEmotions().Select(e => e.Intensity > 0).FirstOrDefault();
                        var Mood = character.CE.Mood > 0;
                        if (!Intensity && !Mood) break;
                        //Console.WriteLine("   " + "Mood: " + character.eS.Mood);
                        //Console.WriteLine("   " + string.Concat(character.eS.GetAllEmotions().Select(e => e.EmotionType + ": " + e.Intensity + " ")));
                    }
                }
            }
            if (!IsPastEvent)///Data set
            {
                if (HaveER)
                    character.Personality.Personalities.ForEach(p => dataFrame.dataTable.Rows.Add(null, null, null, null, null, p));
                dataFrame.SLdocument.ImportDataTable(1, 1, dataFrame.dataTable, true);
                dataFrame.SLdocument.SaveAs(dataFrame.pathFile);
            }
            ///Plots
            if (HaveER)
            {
                ///New form graph
                double[] X = ScottPlot.DataGen.Range(11);
                var pth = @"..\..\..\Results\Graphics\";


                var y = MoodPlot.ToArray();
                var y2 = EmotionPlot.ToArray();
                //var y33 = StrategiesResults.NormalEmotions.ToArray();
                //var y44 = StrategiesResults.NormalMood.ToArray();
                var y3 = NormalEmotionsForScottPlot.ToArray();
                var y4 = NormalMoodForScottPlot.ToArray();

                var avgNormalIntensityOfEmotion = EmotionPlot.Average();
                var avgRegualtedEmotion = EmotionPlot.Average();

                var avgRegulatedMood = MoodPlot.Average();
                var avgNormalMood = NormalEmotionsForScottPlot.Average();

                var pltAVG = new ScottPlot.Plot(1500, 800);
                pltAVG.SetAxisLimits(yMin: 0, yMax: 10, xMin: -0.5, xMax: 11);
                pltAVG.YAxis.RulerMode(true);
                pltAVG.YLabel("Intensidad emocional");
                var yAxis3AVG = pltAVG.AddAxis(ScottPlot.Renderable.Edge.Left, axisIndex: 2, title: "Estado de ánimo");
                yAxis3AVG.LabelStyle(fontSize: 22f);



                string[] SeriesNames = { "Emociones Reguladas", "Emociones Sin Regular" };
                double[][] SeriesValues = { y2, y3 };

                var plt = new ScottPlot.Plot(1500, 800);
                plt.SetAxisLimits(yMin: 0, yMax: 10, xMin: -0.5, xMax: 11);
                plt.YAxis.RulerMode(true);
                plt.YLabel("Intensidad emocional");
                var yAxis3 = plt.AddAxis(ScottPlot.Renderable.Edge.Left, axisIndex: 2, title: "Estado de ánimo");
                yAxis3.LabelStyle(fontSize: 22f);
                yAxis3.LockLimits();

                var RasgosDePersonalidad = new List<string>()
                {
                    "Apertura al cambio, factor – O",
                    "Responsabilidad, factor - C",
                    "Extraversión, factor -E",
                    "Amabilidad, factor -A",
                    "Inestabilidad Emocional, factor -N",
                };

                plt.Legend(true, ScottPlot.Alignment.UpperLeft).FontSize = 17;
                if (character.Personality.DominantPersonality.StartsWith("O"))
                {
                    plt.Title("Rasgo de personalidad: " + RasgosDePersonalidad[0], size: 30);
                }
                else if (character.Personality.DominantPersonality.StartsWith("C"))
                {
                    plt.Title("Rasgo de personalidad: " + RasgosDePersonalidad[1], size: 30);
                }
                else if (character.Personality.DominantPersonality.StartsWith("E"))
                {
                    plt.Title("Rasgo de personalidad: " + RasgosDePersonalidad[2], size: 30);
                }
                else if (character.Personality.DominantPersonality.StartsWith("A"))
                {
                    plt.Title("Rasgo de personalidad: " + RasgosDePersonalidad[3], size: 30);
                }
                else
                {
                    plt.Title("Rasgo de personalidad: " + RasgosDePersonalidad[4], size: 30);
                }
                plt.Grid(true, System.Drawing.Color.Gray);
                plt.XAxis.TickLabelStyle(fontSize: 18);

                ///Bars
                var bars = plt.AddBarGroups(EmotionNamePlot.ToArray(), SeriesNames, SeriesValues, null);
                bars.FirstOrDefault().YAxisIndex = 0;
                bars.FirstOrDefault().FillColor = System.Drawing.Color.LightBlue;
                bars.LastOrDefault().FillColor = System.Drawing.Color.FromArgb(255, 255, 230, 230);

                ///Liner 1
                var line = plt.AddSignalXY(X, y);
                line.YAxisIndex = 2;
                line.LineStyle = ScottPlot.LineStyle.Dash;
                line.Color = System.Drawing.Color.FromArgb(175, 0, 100, 158);
                line.Label = "Estado de ánimo Regulado";
                ///Linear 2
                var line2 = plt.AddSignalXY(X, y4);
                line2.YAxisIndex = 2;
                line2.LineStyle = ScottPlot.LineStyle.Dash;
                line2.Color = System.Drawing.Color.LightCoral;
                line2.Label = "Estado de ánimo Sin Regular";


                plt.SaveFig(pth + character.Personality.DominantPersonality + character.KB.Perspective + ".png");

            }
        }

        static void Main(string[] args)
        {
            Directory.CreateDirectory(@"..\..\..\Results\Graphics"); 

            PersonalityTraits TestPloting = new PersonalityTraits();
            TestPloting.FuzzyPlots();
            //Console.ReadKey();

            var Pedro = BuildRPCharacter("Pedro", "Male");
            Pedro.KB.Tell((Name)"Current(Location)", (Name)"Office", Pedro.RPC.CharacterName);
            Pedro.KB.Tell((Name)"Like(Sarah)", (Name)"True", Pedro.RPC.CharacterName);
            Pedro.EDM.RegisterKnowledgeBase(Pedro.KB);
            var belief = Pedro.RPC.GetAllBeliefs();

            float O = 100f, C = 0f, E = 0f, A = 0f, N = 0f;
            //float O = 0f, C = 100f, E = 0f, A = 0f, N = 0f;
            //float O = 0f, C = 0f, E = 100f, A = 0f, N = 0f;
            //float O = 0f, C = 0f, E = 0f, A = 100f, N = 0f;
            //float O = 0f, C = 0f, E = 0f, A = 0f, N = 100f;

            var li = new List<float[]>()
            {
                new float[] { 0f, 0f, 0f, 0f, 100f },
                new float[] { 0f, 0f, 0f, 100f, 0f },
                new float[] { 0f, 0f, 100f, 0f, 0f },
                new float[] { 0f, 100f, 0f, 0f, 0f },
                new float[] { 100f, 0f, 0f, 0f, 0f },
            };



            Pedro.Personality = new(
                Openness: O, Conscientiousness: C, Extraversion: E, Agreeableness: A, Neuroticism: N);

            var Events = UpdateEvents();
            var PastEvents = Events.Aggregate((k, v) => k.Key == "PastEvents" ? k : v).Value;
            var AlternativeEvents = Events.Aggregate((k, v) => k.Key == "AlternativeEvents" ? k : v).Value;
            var EmotionRegulationEvents = Events.Aggregate((k, v) => k.Key == "ERevents" ? k : v).Value;

            PastEvents.ForEach(Event =>
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.DESIRABILITY, 3, null, Event.GetNTerm(3)));
            AlternativeEvents.ForEach(Event =>
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.LIKE, 3.5f,
                                                            Event.GetNTerm(2).ToString(), Event.GetNTerm(3)));

            var Event = EmotionRegulationEvents.Select(e => e.GetNTerm(3)).ToArray();
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.DESIRABILITY, -6.5f, null, Event[0]);//TalktoBoss
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.DESIRABILITY, 3, null, Event[1]);//Hello
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.PRAISEWORTHINESS, 5, "SELF", Event[2]);//Conversation
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.LIKE, 6.3f, null, Event[3]);//Hug
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.LIKE, -6.6f, null, Event[4]);//Discussion
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.PRAISEWORTHINESS, 3, "SELF", Event[5]);//Congrat
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.DESIRABILITY, -7, null, Event[6]);//Bye
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.LIKE, -9.4f, null, Event[7]);//Fired
            //UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.DESIRABILITY, -8, null, Event[8]);//Crash
            UpdateAppraisalRules(Pedro.EA, OCCAppraisalVariables.DESIRABILITY, 6, null, Event[9]);//Rehired
            List<CompusedAppraisal> robbery = new()
            {
                new CompusedAppraisal { OCC_Variable = OCCAppraisalVariables.DESIRABILITY, Value = -10 },
                new CompusedAppraisal { OCC_Variable = OCCAppraisalVariables.PRAISEWORTHINESS, Value = -6, Target = "SELF" },
            };
            UpdateAppraisalRulesComposed(Pedro.EA, robbery, "Robbery");//Robbery
            List<CompusedAppraisal> _Crash = new()
            {
                new CompusedAppraisal { OCC_Variable = OCCAppraisalVariables.DESIRABILITY, Value = -9 },
                new CompusedAppraisal { OCC_Variable = OCCAppraisalVariables.PRAISEWORTHINESS, Value = -2, Target = "SELF" },
            };
            UpdateAppraisalRulesComposed(Pedro.EA, _Crash, "Crash");//Crash



            foreach (var x in Pedro.EA.GetAllAppraisalRules())
            {
                var newListAppVar = new AppraisalVariables();
                foreach (var appVar in x.AppraisalVariables.appraisalVariables)
                {
                    var newAppVars = new AppraisalVariableDTO();
                    newAppVars.Name = appVar.Name;
                    newAppVars.Target = appVar.Target;
                    newAppVars.Value = appVar.Value;
                    newListAppVar.appraisalVariables.Add(newAppVars);
                }
                var auxRule = new AppraisalRuleDTO
                {
                    AppraisalVariables = newListAppVar,
                    EventMatchingTemplate = x.EventMatchingTemplate,
                };


                auxAgent.m_emotionalAppraisalAsset.AddOrUpdateAppraisalRule(auxRule);
            }



            Pedro.ER = new(
                            eaCharater: Pedro.EA,
                            emotionalStateCharacter: Pedro.CE,
                            Personality: Pedro.Personality,
                            am: Pedro.AM,
                            edm: Pedro.EDM,
                            relatedActions: CreateActions(Pedro.EDM),
                            AlternativeEvents: AlternativeEvents,
                            ExpectedIntensity: 4
                            );



            Console.WriteLine("\n\n\n------------------------ PAST EVENTS --------------------------");
            Simulations(Pedro, PastEvents, IsPastEvent: true, HaveER: false);

            Console.WriteLine("\n\n\n------------------------ CURRENT EVENTS --------------------------");
            Simulations(Pedro, EmotionRegulationEvents, IsPastEvent: false, HaveER: true);
        }
    }
}
