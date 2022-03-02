using System;
using System.Collections.Generic;
using System.Text;
using EmotionalAppraisal.DTOs;
using WellFormedNames;
using IntegratedAuthoringTool;

namespace EmotionRegulation.Components
{

    public class RequiredData
    {
        public List<AppraisalRuleDTO> EventsToAvoid { set; get; }
        public ActionsforEvent ActionsForEvent { set; get; }
        public List<Name> EventsToReappraisal { set; get; }

        public IntegratedAuthoringToolAsset IAT_FAtiMA { get; set; }
    }
}
