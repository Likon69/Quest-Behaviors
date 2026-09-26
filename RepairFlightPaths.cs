// RepairFlightPaths - restores the flight-path connections the engine loses on every restart.
//
// WHY:
// Taxi node names carry a comma ("Lakeshire, Redridge", "Stormwind, Elwynn"). The engine writes a
// node's Connections to Settings\FlightPaths_<char>.xml joined with "," and splits on "," when it
// loads them at bot start, so "Stormwind, Elwynn" comes back as "Stormwind" and "Elwynn", neither
// of which is a node name. Styx.Logic.FlightPaths then finds no route from that node and the bot
// rides instead of flying. A node is only rebuilt when the bot opens the taxi map there during its
// own Fly POI, so the damage persists across sessions.
//
// WHAT IT DOES:
// For every known node, any comma-named node whose pieces are all in its Connections is put back
// as one name, and the pieces are dropped. Pieces of nodes the character never visited are left
// alone (the engine cannot fly to those anyway). In-memory only; the engine rewrites the file the
// next time it reads a taxi map. Idempotent, so every profile can run it at the top.
//
// USAGE:
// <CustomBehavior File="RepairFlightPaths" />

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Styx.Helpers;
using Styx.Logic;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Questing;


namespace Styx.Bot.Quest_Behaviors
{
    public class RepairFlightPaths : CustomForcedBehavior
    {
        public RepairFlightPaths(Dictionary<string, string> args)
            : base(args)
        {
        }


        private bool _isBehaviorDone;

        public override string SubversionId { get { return ("$Id: RepairFlightPaths.cs 1 2026-09-23 endgor $"); } }
        public override string SubversionRevision { get { return ("$Revision: 1 $"); } }


        #region Overrides of CustomForcedBehavior

        public override bool IsDone
        {
            get { return _isBehaviorDone; }
        }


        public override void OnStart()
        {
            try
            {
                Repair();
            }
            catch (Exception except)
            {
                Logging.Write(Color.Orange, "[RepairFlightPaths] skipped: " + except.Message);
            }

            _isBehaviorDone = true;
        }

        #endregion


        private static void Repair()
        {
            List<XmlFlightNode> nodes = FlightPaths.XmlNodes;
            if (nodes == null || nodes.Count == 0)
            {
                Logging.Write(Color.DarkGray, "[RepairFlightPaths] no saved flight nodes yet.");
                return;
            }

            int fixedCount = RepairNodes(nodes);
            HashSet<string> names = new HashSet<string>(nodes.Where(n => !string.IsNullOrEmpty(n.Name)).Select(n => n.Name));
            string routes = string.Join("; ", nodes
                .Where(n => n.MasterEntry != 0)
                .Select(n => n.Name + " -> " + string.Join(" | ", n.Connections.Where(names.Contains).OrderBy(c => c))));
            Logging.Write(Color.CornflowerBlue, "[RepairFlightPaths] restored " + fixedCount + " connection(s). Usable routes: " + routes);
        }


        public static int RepairNodes(List<XmlFlightNode> nodes)
        {
            HashSet<string> names = new HashSet<string>(nodes.Where(n => !string.IsNullOrEmpty(n.Name)).Select(n => n.Name));
            List<string> commaNames = names.Where(n => n.Contains(",")).ToList();
            int fixedCount = 0;

            foreach (XmlFlightNode node in nodes)
            {
                if (node.Connections == null)
                    continue;

                foreach (string full in commaNames)
                {
                    if (full == node.Name || node.Connections.Contains(full))
                        continue;

                    string[] pieces = full.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                    if (pieces.Length < 2 || !pieces.All(p => node.Connections.Contains(p)))
                        continue;

                    node.Connections.Add(full);
                    fixedCount++;
                }

                // Drop the loose pieces that are now covered by a restored name and are not node names themselves.
                HashSet<string> covered = new HashSet<string>(node.Connections
                    .Where(c => c.Contains(","))
                    .SelectMany(c => c.Split(',').Select(p => p.Trim())));
                node.Connections.RemoveWhere(c => !names.Contains(c) && covered.Contains(c));
            }

            return fixedCount;
        }
    }
}
