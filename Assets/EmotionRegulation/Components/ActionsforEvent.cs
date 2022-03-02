using System.Collections.Generic;
using WellFormedNames;
using ActionLibrary.DTOs;
using ActionLibrary;
using EmotionalAppraisal.DTOs;
using FLS.Rules;
using System.Linq;

namespace EmotionRegulation.Components
{
    public class ActionsforEvent
    {
        private List<KeyValuePair<string, float>> NameActions;
        private AppraisalRuleDTO nameEventToReact;
        private ActionRule actionRule;
        private List<ActionRuleDTO> dtoList;
        private ActionRuleDTO setAction;
        private string actionName;
    


        public AppraisalRuleDTO NameEventToReact { get => nameEventToReact; set => nameEventToReact = value; }
        public List<KeyValuePair<string, float>> ActionsForEventER { get; set; }
        public ActionRule ActionRule { get => actionRule; set => actionRule = value; }
        public ActionRuleDTO GetActionRule { get => setAction; }

        public string ActionName { get => actionName; set => SetAction(value); }


        void SetActions(List<KeyValuePair<string, float>> actions)
        {
            dtoList = new List<ActionRuleDTO>();
            var oldAction = nameEventToReact.EventMatchingTemplate.GetNTerm(3);
            foreach (var action in actions)
            {
                var ruleDTO = new ActionRuleDTO
                {
                    Action = Name.BuildName(action + " in " + oldAction),

                };
                actionRule = new ActionRule(ruleDTO);
            }
        }

        void SetAction(ActionRuleDTO action)
        {
            var oldAction = nameEventToReact.EventMatchingTemplate.GetNTerm(3);
            var ruleDTO = new ActionRuleDTO
            {
                Action = Name.BuildName(action + " in " + oldAction),

            };
            setAction = ruleDTO;
        }
        void SetAction(string actionName)
        {
            this.actionName = actionName;
            var oldAction = nameEventToReact.EventMatchingTemplate.GetNTerm(3);
            var ruleDTO = new ActionRuleDTO
            {
                Action = Name.BuildName(actionName + " in " + oldAction),

            };
            setAction = ruleDTO;
        }
    }
}
