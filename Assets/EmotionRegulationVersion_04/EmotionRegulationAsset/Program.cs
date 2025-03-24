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
using IntegratedAuthoringTool;
using DocumentFormat.OpenXml.Bibliography;
using FLS;

namespace EmotionRegulationAsset
{

    class Program
    {
        static RolePlayCharacterAsset auxAgent = new RolePlayCharacterAsset 
        { 
            CharacterName = (Name)"Pedro", m_emotionalAppraisalAsset = new EmotionalAppraisalAsset() 
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


        /// <summary>
        /// /jfsdfgjkdjfkgjdlfjñgds
        /// </summary>
        /// <param name="actionEvent"></param>
        /// <returns></returns>



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

            var origen = @"B:\usr\VSRepos\FAtiMA-Version-01\Assets\EmotionRegulation\EmotionRegulationAsset\Results\";
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
            /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////   
            ///     Escenario: Un día normal en la oficina de trabajo de un agente, donde se encontrará con diferentes eventos 
            ///     al trascurrir el día:
            ///     <!--Evento 1--> 'TalkToBoos'
            ///         Al llegar a la oficina, el agente es notificado para que se presente con su jefe inmediato, debido a un 
            ///         problema referente a su rendimiento laboral.
            ///     <!--Evento 2--> 'Hello'
            ///         ´Después de haber asistido con su jefe, el agente se encuentra con una compañera de trabajo, de la cual siente
            ///         atracción, y ella lo saluda con un ¡Hola!
            ///     <!--Evento 3--> 'Conversation'
            ///         El saludo de la compañera de oficina desencadena una charla con el agente y dicha compañera de trabajo.
            ///     <!--Evento 4--> 'Hug'
            ///         Al despedirse, la compañera de da un abrazo al agente.
            ///     <!--Evento 5--> 'Discussion'
            ///         Transcurrido cierto tiempo, el agente se encuentra en medio de una discusión con otros compañeros de la 
            ///         oficina.
            ///     <!--Evento 6--> 'Congrat'
            ///         Después de haber discutido con sus compañeros, pasado un tiempo, el agente es felicitado por otros compañeros
            ///         debido a un trabajo reciente que acaba de realizar.
            ///     <!--Evento 7--> 'Bye'
            ///         Al final de la jornada laboral, la compañera por la cual siente un especial afecto, anuncia su renuncia 
            ///         laboral debido un cambio de ciudad al agente.
            ///     <!--Evento 8--> 'Fired'
            ///         Antes de retirase de la oficina, el agente es llamado por su jefe, y es despedido.
            ///     <!--Evento 9--> 'Crash'
            ///         Al dirigirse a su departamento, el agente sufre un percance automovilístico, y su carro se averia.
            ///     <!--Evento 10--> 'Rehired'
            ///         El agente llega a una tienda para comprar una bebida, y a su vez compra un billete de lotería, éste sale
            ///         premiado con una considerable suma de dinero.
            ///     <!--Evento 11--> 'BecomeRich'
            ///         Debido al premio de la lotería el agente se puede dar ciertos lujos.
            /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////     

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
                var emotionalState = character.CE.GetAllEmotions().OrderBy(x1=>x1.CauseId);
                Console.WriteLine(" \n  Mood on tick '" + character.AM.Tick + "': " + character.CE.Mood);
                Console.WriteLine("  Active Emotions \n  "
                        + string.Concat(emotionalState.Select(e => e.EmotionType + ": " + e.Intensity + " ")));

                Console.WriteLine("\n  Auxiliar agent Mood: "+ auxAgent.Mood);
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
                        EMOTION  = OredendEmotions.Select(e => e.EmotionType).LastOrDefault();
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
                var pth = @"B:\usr\VSRepos\FAtiMA-Version-01\Assets\EmotionRegulation\EmotionRegulationAsset\Results\Graphics\";


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

            //PersonalityTraits TestPloting = new PersonalityTraits();
            //TestPloting.FuzzyPlots();
            //Console.ReadKey();

            var Pedro = BuildRPCharacter("Pedro", "Male");
            Pedro.KB.Tell((Name)"Current(Location)", (Name)"Office", Pedro.RPC.CharacterName);
            Pedro.KB.Tell((Name)"Like(Sarah)", (Name)"True", Pedro.RPC.CharacterName);
            Pedro.EDM.RegisterKnowledgeBase(Pedro.KB);
            var belief = Pedro.RPC.GetAllBeliefs();


            /*
                Extraversion = 15, 
                Agreeableness = 40, 
                Conscientiousness = 75, 
                Neuroticism = 10, 
                Openness = 45,
                MaxLevelEmotion = 4
             */

            //float O = 100f, C = 0f, E = 0f, A = 0f, N = 0f;
            //float O = 0f, C = 100f, E = 0f, A = 0f, N = 0f;
            //float O = 0f, C = 0f, E = 100f, A = 0f, N = 0f;
            //float O = 0f, C = 0f, E = 0f, A = 100f, N = 0f;
            float O = 0f, C = 0f, E = 0f, A = 0f, N = 100f;

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

