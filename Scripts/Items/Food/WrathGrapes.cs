using System;

namespace Server.Items
{
    public class WrathGrapes : BaseMagicalFood
    {
        [Constructable]
        public WrathGrapes()
            : base(0x2FD7)
        {
            this.Weight = 1.0;
            this.Hue = 0x482;
        }

        public WrathGrapes(Serial serial)
            : base(serial)
        {
        }

        public override MagicalFood FoodID
        {
            get
            {
                return MagicalFood.WrathGrapes;
            }
        }
        public override TimeSpan Cooldown
        {
            get
            {
                return TimeSpan.FromMinutes(2);
            }
        }
        public override TimeSpan Duration
        {
            get
            {
                return TimeSpan.FromSeconds(20);
            }
        }
        public override int EatMessage
        {
            get
            {
                return 1074847;
            }
        }// The grapes of wrath invigorate you for a short time, allowing you to deal extra damage.
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write((int)0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }
}