using System;
using System.Collections.Generic;
using Server.Engines.Quests;
using VitaNex;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Custom
{
    public class QuestStringHolder
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string MobileName { get; set; }
        public string QuestType { get; set; }
        public string Objectives { get; set; }
        public Type TheType { get; set; }

        public QuestStringHolder(Type theType, string name, string description, string mobileName, string questType, string objectives)
        {
            TheType = theType;
            Name = name;
            Description = description;
            MobileName = mobileName;
            QuestType = questType;
            Objectives = objectives;
        }
    }

    public static class Quests
    {
        public static bool DoesContain<TObj>(this TObj[] list, TObj obj)
        {
            return IndexOf(list, obj) != -1;
        }

        public static int IndexOf<TObj>(this IEnumerable<TObj> list, TObj obj)
        {
            return IndexOf(list, obj, null);
        }

        public static int IndexOf<TObj>(this IEnumerable<TObj> list, TObj obj, Func<TObj, bool> match)
        {
            if (list == null)
            {
                return -1;
            }

            int index = -1;
            bool found = false;

            foreach (var o in list)
            {
                ++index;

                if (match != null && match(o))
                {
                    found = true;
                    break;
                }

                if (obj == null || (!ReferenceEquals(o, obj) && !o.Equals(obj)))
                {
                    continue;
                }

                found = true;
                break;
            }

            return found ? index : -1;
        }

        public static List<QuestStringHolder> QuestHolders; 
         
        public static void Initialize()
        {
            CommandUtility.Register("ClilocQuest", AccessLevel.Administrator, CQCommand);
        }

        private static Mobile findQuester(Type quest)
        {
            foreach (var mobile in questStarters)
            {
                if (mobile is BaseQuester && ((BaseQuester) mobile).ParentQuestSystem == quest) return mobile;
                try
                {
                    if (mobile is MondainQuester && ((MondainQuester) mobile).Quests != null)
                    {
                        if (((MondainQuester)mobile).Quests.DoesContain(quest)) return mobile;
                    }
                }
                catch
                {
                }
            }
            
            return null;
        }

        private static List<Mobile> questStarters;

        private static void CQCommand(CommandEventArgs e)
        {
            questStarters = new List<Mobile>();
            foreach (var mobiletype in World.Mobiles.Values)
            {
                if (!questStarters.Contains(mobiletype) &&
                    (mobiletype is BaseQuester && ((BaseQuester) mobiletype).DoesOffer) ||
                    (mobiletype is MondainQuester && ((MondainQuester) mobiletype).Quests != null))
                {
                    questStarters.Add(mobiletype);
                }
            }

            QuestHolders = new List<QuestStringHolder>();
            foreach (var quest in QuestSystem.QuestTypes)
            {
                QuestSystem questSystem = (QuestSystem) Activator.CreateInstance(quest);
                if (questSystem != null)
                {
                    string name = Lookup(questSystem.Name);
                    string offermessage;
                    try { offermessage = Lookup(questSystem.OfferMessage); }
                    catch { offermessage = "Unknown Offer Message"; }
                    string mobileName = "Unknown Mobile"; 
                    string objectives = "";

                    if (questSystem.TypeReferenceTable != null)
                        foreach (var objType in questSystem.TypeReferenceTable)
                        {
                            try
                            {
                                if (objType.IsSubclassOf(typeof (QuestObjective)))
                                {
                                    QuestObjective objective = (QuestObjective) Activator.CreateInstance(objType);
                                    if (objective != null)
                                        objectives += Lookup(objective.Message) + "\n\n";
                                    else
                                    {
                                        Console.WriteLine("QuestObjective null for {0}", objType);
                                    }
                                }
                            }
                            catch
                            {
                                objectives += string.Format("Unknown Objective for {0}\n\n", objType);
                            }
                        }
                    Mobile quester = findQuester(quest);
                    if (quester != null) mobileName = quester.Name;

                    QuestHolders.Add(new QuestStringHolder(quest, name, offermessage, mobileName, "System", objectives));
                }
            }

            foreach (var assembly in ScriptCompiler.Assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.BaseType == typeof(BaseQuest))
                    {
                        BaseQuest baseQuest = (BaseQuest)Activator.CreateInstance(type);
                        if (baseQuest != null)
                        {
                            string name = Lookup(baseQuest.Title);
                            string description = Lookup(baseQuest.Description);
                            string mobileName = "Unknown Mobile";
                            string objectives = "";
                            try
                            {
                                foreach (var objective in baseQuest.Objectives)
                                {
                                    if (objective != null)
                                        objectives += GetObjective(objective);
                                }
                            }
                            catch
                            {
                                objectives = "Unknown Objectives";
                            }
                            Mobile quester = baseQuest.StartingMobile;
                            if (quester == null) quester = findQuester(type);
                            if (quester != null) mobileName = quester.Name;

                            QuestHolders.Add(new QuestStringHolder(type, name, description, mobileName, "Base", objectives));
                        }
                    }
                }
            }

            Console.WriteLine("There were {0} quests found.", QuestHolders.Count);
            PlayerMobile pm = (PlayerMobile) e.Mobile;

            e.Mobile.SendGump(new QuestSearch(e.Mobile, pm));
        }

        private static string GetObjective(BaseObjective objective)
        {
            string objectivestring = "";
            if (objective is SlayObjective)
                objectivestring += "Slay " + ((SlayObjective)objective).Name + "\n";
            else if (objective is ObtainObjective)
                objectivestring += "Obtain " + ((ObtainObjective)objective).Name + "\n";
            else if (objective is EscortObjective)
                objectivestring += ((EscortObjective)objective).Region + " Escort\n";
            else if (objective is DeliverObjective)
                objectivestring += "Deliver " + ((DeliverObjective)objective).DeliveryName + "\n";
            else if (objective is ApprenticeObjective)
                objectivestring += ((ApprenticeObjective)objective).Skill + " Apprenticeship\n";

            return objectivestring;
        }

        private static string Lookup(object o)
        {
            string lookup = "";

            if (o is int)
            {
                ClilocInfo cliloc = Clilocs.Tables[ClilocLNG.ENU].Lookup((int)o);
                if (cliloc != null)
                {
                    lookup = cliloc.Text;
                }
                else
                {
                    lookup = ((int)o).ToString();
                }
            }
            else if (o is string)
            {
                lookup = (string)o;
            }
            return lookup;
        }
    }


    public class QuestSearch : Gump
    {
        public const int LabelHue = 0x480;
        public const int GreenHue = 0x40;
        public const int RedHue = 0x20;

        private Mobile mFrom;
        private PlayerMobile mPlayer;

        private int mPage;
        private int mIndex;
        private Mobile _QuestMobile;
		bool _active;
		bool _completed;
		bool _notstarted;
		bool _questsystem;
		bool _basequest;
		string _searchphrase;

        public QuestSearch(Mobile from, PlayerMobile mobile)
            : this(from, mobile, 1)
        {
        }

        public QuestSearch(Mobile from, PlayerMobile mobile, int page)
            : this(from, mobile, page, 0, 50, 50, true, true, true, true, true, false, "")
        {
        }

        public QuestSearch(Mobile from, PlayerMobile mobile, int page, int index, int xPos, int yPos,
            bool active, bool completed, bool notstarted, bool basequests, bool questsystems, bool details, string searchphrase)
            : base(xPos, yPos)
        {
            mFrom = from;
            mPlayer = mobile;
            mPage = page;
            mIndex = index;
			_active = active;
			_completed = completed;
			_notstarted = notstarted;
			_questsystem = basequests;
			_basequest = questsystems;
			_searchphrase = searchphrase;
            _QuestMobile = null;

            int y = 75;
            List<QuestStringHolder> holders = FilteredHolders(searchphrase, active, completed, notstarted, basequests, questsystems);

            AddPage(0);

            AddBackground(0, 0, 250, 230, 5054);
            AddAlphaRegion(1, 1, 248, 228);
            AddBackground(0, 231, 250, 269, 5054);
            AddAlphaRegion(1, 232, 248, 267);

            if (details)
            {
                AddBackground(251, 0, 504, 500, 5054);
                AddAlphaRegion(252, 1, 502, 498);
                AddDetail(holders[mIndex]);
            }

            if (mPlayer != null)
            {
                AddLabel(23, 26, 0x384, mPlayer.Name);

                y = 13;
                AddCheck(133, y, 0xD2, 0xD3, active, 6);
                AddLabel(156, y, 0x384, "Active");

                y += 30;
                AddCheck(133, y, 0xD2, 0xD3, completed, 7);
                AddLabel(156, y, 0x384, "Completed");

                y += 30;
                AddCheck(133, y, 0xD2, 0xD3, notstarted, 8);
                AddLabel(156, y, 0x384, "Not Started");

                y = 75;
            }

            AddButton(5, y, 0xFA8, 0xFAA, 5, GumpButtonType.Reply, 0);
            AddLabel(38, y, 0x384, "Select Player");

            y += 30;
            AddCheck(5, y, 0xD2, 0xD3, questsystems, 9);
            AddLabel(28, y, 0x384, "Quest System");

            y += 30;
            AddCheck(5, y, 0xD2, 0xD3, basequests, 10);
            AddLabel(28, y, 0x455, "Base Quest");

            y += 30;
            AddImageTiled(5, y, 200, 19, 0xBBC);
            AddTextEntry(5, y, 200, 19, 0, 11, searchphrase);

            y += 30;
            AddButton(5, y, 0xFA8, 0xFAA, 4, GumpButtonType.Reply, 0);
            AddLabel(38, y, 0x384, "Apply Filters");
            y += 10;

            if (holders.Count > 0)
            {
                int maxpages = (int)Math.Ceiling((decimal)holders.Count/8);
                int highestindex = holders.Count/mPage >= 8 ? 8 : holders.Count - ((maxpages - 1)*8);
                if (mPage > 1)
                    AddButton(6, 246, 250, 251, 15, GumpButtonType.Reply, 0); // Go back 1 page
                if (mPage < maxpages)
                    AddButton(6, 460, 252, 253, 16, GumpButtonType.Reply, 0); // Go forward 1 page

                for (int x = 0; x < highestindex; x++)
                {
					int loopindex = (mPage - 1)*8 + x;
                    bool now = (loopindex == mIndex && details);
                    y += 30;
                    if (now)
                        AddImage(35, y, 2154);
                    else
                        AddButton(35, y, 2152, 2152, 1000 + loopindex, GumpButtonType.Reply, 0); // Show details
                    AddLabel(69, y + 5, holders[loopindex].QuestType == "System" ? 0x384 : 0x455,
                        holders[loopindex].Name);
                }
            }
        }

        private List<QuestStringHolder> FilteredHolders(string search, bool active, bool completed, bool notstarted, bool basequests, bool questsystems)
        {
            List<QuestStringHolder> holders = new List<QuestStringHolder>();
			if (search.Length > 0)
			{
				foreach (var quest in Quests.QuestHolders)
				{
				    if (!holders.Contains(quest) &&
				        (Insensitive.Contains(quest.Description, search) || Insensitive.Contains(quest.Name, search) ||
				         (quest.MobileName != null && Insensitive.Contains(quest.MobileName, search))))
				        holders.Add(quest);
				}
			}
			else
				holders = Quests.QuestHolders;
			List<QuestStringHolder> playerHolders = new List<QuestStringHolder>();
			if (!active || !completed || !notstarted)
			{
				if (active)
				{
					foreach (var quest in holders)
					{
						if (!playerHolders.Contains(quest) && IsActiveQuest(mPlayer, quest)) playerHolders.Add(quest);
                    }
				}
				if (completed)
				{
					foreach (var quest in holders)
					{
						if (!playerHolders.Contains(quest) && IsCompletedQuest(mPlayer, quest)) playerHolders.Add(quest);
					}
				}
				if (notstarted)
				{
					foreach (var quest in holders)
					{
						if (!playerHolders.Contains(quest) && 
							(!IsActiveQuest(mPlayer, quest) && !IsCompletedQuest(mPlayer, quest))) playerHolders.Add(quest);
					}
				}
			}
			else
				playerHolders = holders;
			holders = new List<QuestStringHolder>();
			if (basequests)
			{
				foreach (var quest in playerHolders)
				{
					if (!holders.Contains(quest) && quest.QuestType == "Base") holders.Add(quest);
				}
			}
			if (questsystems)
			{
				foreach (var quest in playerHolders)
				{
					if (!holders.Contains(quest) && quest.QuestType == "System") holders.Add(quest);
				}
			}
            if (active)
            {
                List<Item> items = XmlQuest.FindXmlQuest(mPlayer);
                if (items != null && items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        if (item is XmlQuestHolder)
                        {
                            XmlQuestHolder xml = (XmlQuestHolder)item;
                            holders.Add(new QuestStringHolder(xml.GetType(), xml.Name, xml.Description1, "XML", "Xml",
                                xml.Objective1));
                        }
                    }
                }
            }

            return holders;
        }
		
		private bool IsActiveQuest(PlayerMobile pm, QuestStringHolder holder)
		{
		    if (pm.Quests != null)
		    {
		        foreach (var quest in pm.Quests)
		        {
		            if (quest.GetType() == holder.TheType) return true;
		        }
		    }
			return false;
		}
		
		private bool IsCompletedQuest(PlayerMobile pm, QuestStringHolder holder)
		{
		    if (pm.DoneQuests != null)
		    {
		        foreach (var quest in pm.DoneQuests)
		        {
		            if (quest.QuestType == holder.TheType) return true;
		        }
		    }
			return false;
		}

        public void AddHtml(int x, int y, int width, int height, string text, int color, bool background,
            bool scrollbar)
        {
            AddHtml(x, y, width, height, Color(text, color), background, scrollbar);
        }

        public string Color(string text, int color)
        {
            return String.Format("<BASEFONT COLOR=#{0:X6}>{1}</BASEFONT>", color, text);
        }

        private void AddDetail(QuestStringHolder quest)
        {
            AddLabel(275, 5, GreenHue, quest.QuestType == "Base" ? "Base Quest Title" 
                : quest.QuestType == "System" ? "Quest System Name"
                : quest.QuestType == "Xml" ? "Xml Quest Name" 
                : "Unknown Quest Type");
            AddHtml(255, 25, 225, 30, quest.Name, 0x20, true, false);
            AddLabel(515, 5, GreenHue, "Mobile Name");
            AddHtml(485, 25, 200, 30, quest.MobileName, 0x20, true, false);
            _QuestMobile = FindMobile(quest.MobileName);
            if (_QuestMobile != null)
            {
                AddButton(690, 25, 1210, 1210, 25, GumpButtonType.Reply, 0);
                AddLabel(706, 25, GreenHue, "Go");
            }
            AddLabel(275, 60, GreenHue, quest.QuestType == "System" ? "Offer Message" : "Description");
            AddHtml(255, 80, 490, 130, quest.Description, 0x20, true, true);
            AddLabel(275, 215, GreenHue, "Objectives");
            AddHtml(255, 235, 490, 260, quest.Objectives, 0x20, true, true);
        }

        private Mobile FindMobile(string name)
        {
            foreach (var mobile in World.Mobiles.Values)
            {
                if (mobile.Name == name && (mobile is BaseQuester || mobile is MondainQuester)
                    && mobile.Map != Map.Internal)
                    return mobile;
            }
            return null;
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            // Apply Filters
            if (info.ButtonID == 4)
            {
                bool active = info.IsSwitched(6);
                bool completed = info.IsSwitched(7);
                bool notstarted = info.IsSwitched(8);
                bool questsystem = info.IsSwitched(9);
                bool basequest = info.IsSwitched(10);
                string searchtext = info.GetTextEntry(11).Text;

                if (mPlayer != null && !active && !completed && !notstarted)
                {
                    active = true;
                    mFrom.SendMessage("At least one of the top 3 boxes must be checked.");
                }

                if (!questsystem && !basequest)
                {
                    questsystem = true;
                    mFrom.SendMessage("At least one of the 2 quest types must be checked.");
                }
                mFrom.SendGump(new QuestSearch(mFrom, mPlayer, 1, 0, X, Y, active, completed, notstarted,
                    basequest, questsystem, false, searchtext));
            }

            // Select Player
            else if (info.ButtonID == 5)
            {
                mFrom.Target = new PlayerTarget();
            }

            // Go Back 1 Page
            else if (info.ButtonID == 15)
            {
                mFrom.SendGump(new QuestSearch(mFrom, mPlayer, mPage - 1, 0, X, Y,
					_active, _completed, _notstarted, _questsystem, _basequest, false, _searchphrase));
            }

            // Go Forward 1 Page
            else if (info.ButtonID == 16)
            {
                mFrom.SendGump(new QuestSearch(mFrom, mPlayer, mPage + 1, 0, X, Y, 
					_active, _completed, _notstarted, _questsystem, _basequest, false, _searchphrase));
            }

            // Teleport to the Quest Mobile
            else if (info.ButtonID == 25)
            {
                try
                {
                    mFrom.MoveToWorld(_QuestMobile.Location, _QuestMobile.Map);
                    mFrom.PlaySound(0x1FE);
                    mFrom.SendMessage("You have been transported to the Quest Mobile.");
                    mFrom.SendGump(new QuestSearch(mFrom, mPlayer, mPage, mIndex, X, Y,
                        _active, _completed, _notstarted, _questsystem, _basequest, true, _searchphrase));
                }
                catch
                {
                    mFrom.SendMessage("There was an error transporting to the Quest Mobile.");
                    mFrom.SendGump(new QuestSearch(mFrom, mPlayer, mPage, mIndex, X, Y,
                        _active, _completed, _notstarted, _questsystem, _basequest, true, _searchphrase));
                }
            }

            // Show details
            else if (info.ButtonID > 999)
            {
                mFrom.SendGump(new QuestSearch(mFrom, mPlayer, mPage, info.ButtonID - 1000, X, Y, 
					_active, _completed, _notstarted, _questsystem, _basequest, true, _searchphrase));
            }
        }

        private class PlayerTarget : Target
        {
            public PlayerTarget() : base(3, false, TargetFlags.None)
            {
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is PlayerMobile)
                {
                    PlayerMobile pm = (PlayerMobile) targeted;
                    from.SendGump(new QuestSearch(from, pm));
                }
                else
                {
                    from.SendMessage("Invalid target. Please select a Player, or target yourself.");
                    from.Target = new PlayerTarget();
                }
            }
        }
    }
}
