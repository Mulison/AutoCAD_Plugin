using Autodesk.AutoCAD.Runtime;
//using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Linq.Expressions;

namespace move_lines_to_layer
{
    public class MoveLinesToLayer
    {
        [CommandMethod("MOVE_LINES_TO_LAYER")]

        public void MoveLinesToLayerFunction()
        {
            // 0) Get current document / database / editor 
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1) get all the entities (select objects)
            PromptSelectionResult psr = ed.GetSelection();
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nNo objects selected.");
                return;
            }

            // 2) get the target layer from user (Benutzer nach Ziel-Layer fragen)
            PromptStringOptions pso = new PromptStringOptions("\nEnter target layer name: ")
            {
                AllowSpaces = true
            };
            PromptResult pr = ed.GetString(pso);
            if (pr.Status != PromptStatus.OK) return;

            string targetLayerName = pr.StringResult.Trim();
            if (string.IsNullOrWhiteSpace(targetLayerName))
            {
                ed.WriteMessage("\nNo valid layer name entered.");
                return;
            }

            // 3) Transaction: move lines to layer
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 3.1) check whether the target layer exists
                LayerTable It = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                ObjectId targetLayerId;
                if (!It.Has(targetLayerName))
                {
                    It.UpgradeOpen();
                    LayerTableRecord Itr = new LayerTableRecord
                    {
                        Name = targetLayerName
                    };
                    targetLayerId = It.Add(Itr);
                    tr.AddNewlyCreatedDBObject(Itr, true);
                    ed.WriteMessage($"\nLayer \"{targetLayerName}\" created.");
                }
                else
                {
                    targetLayerId = It[targetLayerName];
                }

                // 3.2) move selected entities to target layer
                int total = 0, changed = 0, skipped = 0;
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    total++;
                    try
                    {
                        Entity? ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent == null) { skipped++; continue; }


                        // Nur LINIE? -> Optional: if (!(ent is Line)) continue;
                        if (ent.LayerId != targetLayerId)
                        {
                            ent.LayerId = targetLayerId;
                            changed++;
                        }
                    }
                    catch
                    {
                        // z.B. XRef, gesperrte Layer etc.
                        skipped++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\nDone: {changed}/{total} moved to \"{targetLayerName}\". Skipped: {skipped}.");
            }
        }

    }
}
