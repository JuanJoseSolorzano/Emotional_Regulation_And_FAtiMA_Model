using System;
using System.Collections.Generic;
using System.Text;

namespace EmotionRegulation.BigFiveModel
{
    public class PersonalityDTO
    {
        public float Openness { get; set; }
        public float Conscientiousness { get; set; }
        public float Extraversion { get; set; }
        public float Agreeableness { get; set; }
        public float Neuroticism { get; set; }
        public float MaxLevelEmotion { get; set; }

        public List<float> ToList()
        {
            List<float> personality_List = new List<float>()
            {
                this.Openness, this.Conscientiousness, this.Extraversion, this.Agreeableness, this.Neuroticism
            };

            return personality_List;
        }
    }


    
}
