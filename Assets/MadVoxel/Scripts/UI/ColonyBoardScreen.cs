using System.Collections.Generic;
using MadVoxel.Colony;
using MadVoxel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>
    /// The colony's only screen: a Claim Slate plaque. Found the colony, name it, read
    /// the two numbers that decide whether anyone stays, set each person's job, and
    /// order shelter before dusk.
    ///
    /// There is no second HUD for the colony on purpose. Everything here is something
    /// you would walk to a board to read, and nothing here needs to be in your eye
    /// while you are digging.
    /// </summary>
    public class ColonyBoardScreen : MonoBehaviour
    {
        const float RowHeight = 64f;
        const float RowGap = 6f;

        ColonyWorld _colony;

        Canvas _canvas;
        RectTransform _panel;
        RectTransform _roster;
        InputField _nameField;
        Text _summary, _supplies, _heatLine, _requirements;
        Button _foundButton, _shelterButton, _recruitButton, _turnAwayButton;
        Text _foundLabel, _shelterLabel, _recruitLabel;

        readonly List<RosterRow> _rows = new List<RosterRow>();

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        /// <summary>Set by the session so the board can show the claim's noise.</summary>
        public Claim.ClaimHeatTracker Heat { get; set; }

        class RosterRow
        {
            public Colonist Person;
            public Image Background;
            public Text Name;
            public Text Needs;
            public Button JobButton;
            public Text JobLabel;
        }

        public void Init(ColonyWorld colony)
        {
            _colony = colony;

            _canvas = UIKit.CreateCanvas("ColonyBoard", 12, transform);
            var backdrop = ClaimSlate.Surface(_canvas.transform, "Backdrop", ClaimSlate.Fade(ClaimSlate.OilBlack, 0.78f));
            ClaimSlate.Stretch(backdrop.rectTransform);

            Build();
            _canvas.enabled = false;
        }

        void Build()
        {
            var body = ClaimSlate.Plate(_canvas.transform, "BoardPlate", "COLONY BOARD",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 760f), true);
            _panel = (RectTransform)body.parent;

            var stamp = ClaimSlate.Stencil(_panel, "Stamp", "MADGENIUS  -  CHARTER", 15,
                TextAnchor.UpperLeft, ClaimSlate.Dim(ClaimSlate.Bone, 0.34f), false);
            ClaimSlate.Place(ClaimSlate.Holder(stamp), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -58f), new Vector2(480f, 20f));

            _nameField = UIKit.Input(_panel, "Name", "Mad Colony", "Mad Colony");
            ClaimSlate.Place(_nameField.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -20f), new Vector2(420f, 44f));

            _summary = ClaimSlate.Stencil(_panel, "Summary", "", 24, TextAnchor.UpperLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_summary), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -96f), new Vector2(1040f, 28f));

            // The two numbers that decide whether anyone stays.
            _supplies = ClaimSlate.Stencil(_panel, "Supplies", "", 22, TextAnchor.UpperLeft, ClaimSlate.SodiumGold);
            ClaimSlate.Place(ClaimSlate.Holder(_supplies), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -128f), new Vector2(700f, 26f));

            _heatLine = ClaimSlate.Stencil(_panel, "Heat", "", 22, TextAnchor.UpperRight, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(_heatLine), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -128f), new Vector2(420f, 26f));

            _requirements = ClaimSlate.Stencil(_panel, "Requirements", "", 19,
                TextAnchor.UpperLeft, ClaimSlate.Pencil, false);
            ClaimSlate.Place(ClaimSlate.Holder(_requirements), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -160f), new Vector2(1040f, 26f));

            var rule = ClaimSlate.Fill(_panel, "Rule", ClaimSlate.OxideRust);
            ClaimSlate.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -192f), new Vector2(1056f, 2f));

            _roster = ClaimSlate.Rect(_panel, "Roster");
            ClaimSlate.Place(_roster, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -206f), new Vector2(1056f, 380f));

            _foundButton = UIKit.Button(_panel, "Found", "FOUND", 20);
            ClaimSlate.Place(_foundButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(32f, 24f), new Vector2(300f, 48f));
            _foundLabel = _foundButton.GetComponentInChildren<Text>();
            _foundButton.onClick.AddListener(Found);

            _recruitButton = UIKit.Button(_panel, "Recruit", "TAKE IN A WANDERER", 20);
            ClaimSlate.Place(_recruitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(340f, 48f));
            _recruitLabel = _recruitButton.GetComponentInChildren<Text>();
            _recruitButton.onClick.AddListener(Recruit);

            // Only shown while someone is waiting. Turning them away has to be as
            // available as taking them in, or it is not a decision.
            _turnAwayButton = UIKit.Button(_panel, "TurnAway", "TURN THEM AWAY", 16);
            ClaimSlate.Place(_turnAwayButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f), new Vector2(320f, 34f));
            _turnAwayButton.onClick.AddListener(TurnAway);
            _turnAwayButton.gameObject.SetActive(false);

            _shelterButton = UIKit.Button(_panel, "Shelter", "ORDER SHELTER", 20);
            ClaimSlate.Place(_shelterButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 24f), new Vector2(300f, 48f));
            _shelterLabel = _shelterButton.GetComponentInChildren<Text>();
            _shelterButton.onClick.AddListener(ToggleShelter);

            var hint = ClaimSlate.Stencil(_panel, "Hint", "ESC TO CLOSE", 15,
                TextAnchor.LowerRight, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(hint), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 78f), new Vector2(300f, 20f));
        }

        // -------------------------------------------------------------------- open

        public void Open()
        {
            _canvas.enabled = true;
            if (_colony != null && _colony.Founded) _nameField.text = _colony.ColonyName;

            RebuildRoster();
            Refresh();
        }

        public void Close()
        {
            if (_canvas != null) _canvas.enabled = false;
        }

        void Found()
        {
            if (_colony == null) return;

            var result = _colony.TryFound(_nameField.text);
            Notifications.Post(result == FoundResult.Ok
                ? _colony.ColonyName + " founded"
                : ColonyCharter.Describe(result));

            RebuildRoster();
            Refresh();
        }

        /// <summary>
        /// Takes in whoever is at the fence.
        ///
        /// This used to conjure a colonist out of nothing whenever it was pressed,
        /// which is not a system - it is a cheat with a label on it. Now it only does
        /// anything when someone has actually turned up, and the readout beside it says
        /// what the colony is short of when nobody will.
        /// </summary>
        void Recruit()
        {
            if (_colony == null || !_colony.WandererWaiting) return;

            var person = _colony.AcceptWanderer();
            if (person != null) Notifications.PostFormat("{0} stays", person.Name);

            RebuildRoster();
            Refresh();
        }

        void TurnAway()
        {
            if (_colony == null || !_colony.WandererWaiting) return;

            _colony.TurnAwayWanderer();
            Refresh();
        }

        void ToggleShelter()
        {
            if (_colony == null || !_colony.Founded) return;

            _colony.OrderShelter(!_colony.Sheltering);
            Refresh();
        }

        // ------------------------------------------------------------------ roster

        void RebuildRoster()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Background != null) Destroy(_rows[i].Background.gameObject);
            }
            _rows.Clear();

            if (_colony == null || !_colony.Founded) return;

            var people = _colony.Colonists;
            for (int i = 0; i < people.Count; i++) _rows.Add(BuildRow(people[i], i));
        }

        RosterRow BuildRow(Colonist person, int index)
        {
            var card = ClaimSlate.Surface(_roster, "Row" + index, ClaimSlate.Metal);
            ClaimSlate.Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -index * (RowHeight + RowGap)), new Vector2(1056f, RowHeight));
            ClaimSlate.Frame(card.rectTransform, ClaimSlate.Dim(ClaimSlate.Bone, 0.18f), 1f);

            var name = ClaimSlate.Stencil(card.transform, "Name", person.Name.ToUpperInvariant(), 22,
                TextAnchor.MiddleLeft, ClaimSlate.Bone);
            ClaimSlate.Place(ClaimSlate.Holder(name), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, 0f), new Vector2(280f, 26f));

            var needs = ClaimSlate.Stencil(card.transform, "Needs", "", 18,
                TextAnchor.MiddleLeft, ClaimSlate.BoneDim, false);
            ClaimSlate.Place(ClaimSlate.Holder(needs), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(310f, 0f), new Vector2(480f, 24f));

            var jobButton = UIKit.Button(card.transform, "Job", "", 18);
            ClaimSlate.Place(jobButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f), new Vector2(220f, 40f));

            var captured = person;
            jobButton.onClick.AddListener(() => CycleJob(captured));

            return new RosterRow
            {
                Person = person,
                Background = card,
                Name = name,
                Needs = needs,
                JobButton = jobButton,
                JobLabel = jobButton.GetComponentInChildren<Text>()
            };
        }

        /// <summary>
        /// Five jobs, cycled by clicking. A dropdown for five options would be a menu
        /// where a button will do.
        /// </summary>
        static void CycleJob(Colonist person)
        {
            var values = (ColonyJob[])System.Enum.GetValues(typeof(ColonyJob));
            int index = System.Array.IndexOf(values, person.Job);
            person.Job = values[(index + 1) % values.Length];
        }

        // ----------------------------------------------------------------- refresh

        /// <summary>
        /// Five times a second, not sixty. Readiness walks every structure in the claim
        /// counting beds, food and water, and it sits on top of the scans this screen
        /// already did - three full passes a frame for numbers that move on a clock
        /// measured in in-game hours.
        /// </summary>
        void Update()
        {
            if (!IsOpen) return;

            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh < 0.2f) return;

            _sinceRefresh = 0f;
            Refresh();
        }

        float _sinceRefresh;

        public void Refresh()
        {
            if (_colony == null) return;

            bool founded = _colony.Founded;
            int population = _colony.Population;
            int cap = _colony.Rules != null ? _colony.Rules.maxColonists : 3;

            _nameField.interactable = !founded;
            _foundButton.interactable = !founded;
            _foundLabel.text = founded ? "FOUNDED" : "FOUND THIS COLONY";

            // The button is only live when there is someone to answer. The rest of the
            // time it reads as the reason nobody is coming, which is the thing the
            // player can actually do something about.
            bool waiting = _colony.WandererWaiting;

            _recruitButton.interactable = waiting;
            _recruitLabel.text = waiting
                ? "TAKE IN " + _colony.WandererName.ToUpperInvariant()
                : ColonyRecruitment.Describe(_colony.Readiness(), _colony.DaysSinceArrival);

            _turnAwayButton.gameObject.SetActive(waiting);

            _shelterButton.interactable = founded && population > 0;
            _shelterLabel.text = _colony.Sheltering ? "BACK TO WORK" : "ORDER SHELTER";

            if (!founded)
            {
                _summary.text = "UNFOUNDED";
                _summary.color = ClaimSlate.Pencil;
                _supplies.text = "";
                _heatLine.text = "";
                _requirements.text = DescribeRequirements();
                return;
            }

            _summary.text = string.Format("{0}   POP {1}/{2}", _colony.ColonyName.ToUpperInvariant(), population, cap);
            _summary.color = ClaimSlate.Bone;

            float foodDays = ColonyCharter.FoodDays(_colony.FoodInStore(), population, _colony.Rules);
            float waterDays = ColonyCharter.WaterDays(_colony.WaterInStore(), population, _colony.Rules);

            _supplies.text = string.Format("FOOD {0}   WATER {1}",
                ColonyCharter.DescribeDays(foodDays), ColonyCharter.DescribeDays(waterDays));
            _supplies.color = ColonyCharter.IsShort(foodDays) || ColonyCharter.IsShort(waterDays)
                ? ClaimSlate.OxideRust : ClaimSlate.SodiumGold;

            _heatLine.text = Heat != null ? Heat.Readout() : "";
            _heatLine.color = Heat != null && Heat.Heat >= 50f ? ClaimSlate.OxideRust : ClaimSlate.Bone;

            // Not an instruction - there is no button that summons anyone. What the
            // board can usefully say when the colony is empty is what would bring the
            // first person, which is exactly what the recruit line already reads.
            _requirements.text = population == 0
                ? "NOBODY LIVES HERE YET  -  SOMEONE MAY FIND YOU"
                : "";

            if (_rows.Count != population) RebuildRoster();
            for (int i = 0; i < _rows.Count; i++) RefreshRow(_rows[i]);
        }

        void RefreshRow(RosterRow row)
        {
            var person = row.Person;
            if (person == null) return;

            row.JobLabel.text = person.Job.ToString().ToUpperInvariant();
            row.Name.color = person.IsAlive ? ClaimSlate.Bone : ClaimSlate.Pencil;

            string mood = ColonyMorale.Describe(person.Morale, person.Definition);
            row.Needs.text = string.Format("FOOD {0}   WATER {1}   {2}{3}",
                Mathf.RoundToInt(person.Food), Mathf.RoundToInt(person.Water), mood,
                person.Bed == null ? "   NO BED" : "");

            bool struggling = person.IsHungry || person.IsThirsty || person.Bed == null;
            row.Needs.color = struggling ? ClaimSlate.OxideRust : ClaimSlate.BoneDim;
        }

        /// <summary>The checklist, so a refusal tells you what to go and build.</summary>
        string DescribeRequirements()
        {
            var check = _colony.Survey();
            var sb = new System.Text.StringBuilder();

            Append(sb, "CUPBOARD", check.HasCupboard);
            Append(sb, "BED", check.Beds > 0);
            Append(sb, "CAMPFIRE", check.HasCookingFire);
            Append(sb, "WATER", check.HasWater);
            Append(sb, "FOOD", check.FoodItems > 0);
            return sb.ToString();
        }

        static void Append(System.Text.StringBuilder sb, string label, bool have)
        {
            if (sb.Length > 0) sb.Append("    ");
            sb.Append(have ? "[X] " : "[ ] ").Append(label);
        }
    }
}
