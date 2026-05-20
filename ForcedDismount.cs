// Behavior originally contributed by Bobby53.
//
// DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_ForcedDismount
//
// QUICK DOX:
//      Dismounts a toon from a mount (or Druid flying form).
//      If flying, the behavior will attempt to land before dismounting.
//
//  Parameters (required, then optional--both listed alphabetically):
//      QuestId [Default:none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//
using System;
using System.Collections.Generic;
using System.Threading;

using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors
{
    public class ForcedDismount : CustomForcedBehavior
    {
        public ForcedDismount(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
            }
            catch (Exception except)
            {
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (ObjectManager.Me); } }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: ForcedDismount.cs 229 2012-04-25 01:57:29Z natfoth $"); } }
        public override string SubversionRevision { get { return ("$Revision: 229 $"); } }


        ~ForcedDismount()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                base.Dispose();
            }

            _isDisposed = true;
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(
                    // If not mounted and not shapeshifted, nothing to do
                    new Decorator(ret => !Me.Mounted && Me.Shapeshift == ShapeshiftForm.Normal,
                        new Action(ret => _isBehaviorDone = true)),

                    // Druid flight/travel form → cancel form
                    new Decorator(ret => Me.Shapeshift != ShapeshiftForm.Normal,
                        new Action(ret =>
                        {
                            TreeRoot.StatusText = "Cancelling shapeshift form";
                            Lua.DoString("CancelShapeshiftForm()");
                            Thread.Sleep(1000);
                            _isBehaviorDone = true;
                        })),

                    // Normal mount → dismount via Lua
                    new Decorator(ret => Me.Mounted,
                        new Action(ret =>
                        {
                            TreeRoot.StatusText = "Dismounting";
                            // Stop movement first
                            if (Me.IsMoving)
                            {
                                WoWMovement.MoveStop();
                                Thread.Sleep(500);
                            }

                            Lua.DoString("Dismount()");
                            Thread.Sleep(1000);
                            _isBehaviorDone = true;
                        }))
                ));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


        public override void OnStart()
        {
            OnStart_HandleAttributeProblem();

            if (!IsDone)
            {
                TreeRoot.GoalText = "Dismounting";
            }
        }

        #endregion
    }
}
