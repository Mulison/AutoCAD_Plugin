using Autodesk.AutoCAD.Runtime;
//using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace AutoCAD_Plugin
{
    public class MakeBlockCmd
    {
        [CommandMethod("MAKEBLOCK_FROM_SELECTION")]
        public void MakeBlockFromSelection()
        {
            // Get current document / database / editor
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1) Select objects
            PromptSelectionResult psr = ed.GetSelection();
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nNo selection.");
                return;
            }
            ObjectIdCollection idsToClone = new ObjectIdCollection(psr.Value.GetObjectIds());

            // 2) Specify base point (for in-place insertion)
            PromptPointResult ppr = ed.GetPoint(new PromptPointOptions("\nPick base point: "));
            if (ppr.Status != PromptStatus.OK) return;
            Point3d basePt = ppr.Value;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Get model space
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Generate a unique block name
                string blkName = "MySelBlock_" + System.DateTime.Now.ToString("HHmmss");

                // Create new block definition
                BlockTableRecord btr = new BlockTableRecord { Name = blkName };
                bt.UpgradeOpen();
                ObjectId btrId = bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);

                // Same-database clone: copy selected entities into the block definition (instead of WblockCloneObjects)
                IdMapping idMap = new IdMapping();
                db.DeepCloneObjects(idsToClone, btrId, idMap, false);

                // Move block contents so that base point becomes (0,0,0)
                BlockTableRecord btrWrite = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
                Matrix3d toLocal = Matrix3d.Displacement(new Vector3d(-basePt.X, -basePt.Y, -basePt.Z));
                foreach (ObjectId eid in btrWrite)
                {
                    Entity entInBlock = (Entity)tr.GetObject(eid, OpenMode.ForWrite);
                    entInBlock.TransformBy(toLocal);
                }

                // Insert a block reference at the base point
                using (BlockReference bref = new BlockReference(basePt, btrId))
                {
                    bref.ScaleFactors = new Scale3d(1.0);
                    bref.Rotation = 0.0;
                    ms.AppendEntity(bref);
                    tr.AddNewlyCreatedDBObject(bref, true);
                }

                // Delete original objects (achieve “in-place block conversion”)
                foreach (ObjectId id in idsToClone)
                {
                    Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                    ent.Erase();
                }

                tr.Commit();
                ed.WriteMessage($"\nCreated block: {blkName} at {basePt}.");
            }
        }
    }
}
