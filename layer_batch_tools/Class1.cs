using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Colors;


// Mehrere Layer-Eigenschaften auf einmal zu ändern, basierend auf einer Objektauswahl

namespace AutoCAD_Plugin
{
    public class LayerBatchTools
    {
        [CommandMethod("BATCH_SET_LAYER_STYLE")]
        public void BatchSetLayerStyle()
        {
            // Get current document / database / editor
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1) Select objects
            PromptSelectionResult psr = ed.GetSelection();
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nNo objects selected.");
                return;
            }

            // 2) Collect unique layer names
            HashSet<string> layerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;
                    Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    if (!string.IsNullOrWhiteSpace(ent.Layer))
                        layerNames.Add(ent.Layer);
                }
                tr.Commit();
            }

            if (layerNames.Count == 0)
            {
                ed.WriteMessage("\nNo layers found from the selection.");
                return;
            }

            // 3) Ask user: change color? (ACI 0-255, -1 to skip)
            short? targetAci = null;
            {
                PromptIntegerOptions pio = new PromptIntegerOptions(
                    "\nEnter target layer color ACI (0-255), or -1 to skip color change")
                {
                    LowerLimit = -1,
                    UpperLimit = 255,
                    DefaultValue = -1,
                    AllowNegative = true,
                    AllowNone = true
                };
                PromptIntegerResult pir = ed.GetInteger(pio);
                if (pir.Status != PromptStatus.OK) return;
                if (pir.Value >= 0) targetAci = (short)pir.Value; // -1 means skip
            }

            // 4) Ask user: change linetype? (empty to skip)
            string lineTypeName = null;
            {
                PromptStringOptions pso = new PromptStringOptions(
                    "\nEnter target linetype name (e.g. Continuous/Hidden/Center), press Enter to skip")
                {
                    AllowSpaces = false
                };
                PromptResult pr = ed.GetString(pso);
                if (pr.Status != PromptStatus.OK) return;
                if (!string.IsNullOrWhiteSpace(pr.StringResult))
                    lineTypeName = pr.StringResult.Trim();
            }

            if (targetAci == null && string.IsNullOrEmpty(lineTypeName))
            {
                ed.WriteMessage("\nNo properties selected to modify.");
                return;
            }

            // 5) Modify layers in a transaction
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                // Load linetype from acad.lin if not found
                if (!string.IsNullOrEmpty(lineTypeName) && !ltt.Has(lineTypeName))
                {
                    try
                    {
                        db.LoadLineTypeFile(lineTypeName, "acad.lin");
                        ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                        ed.WriteMessage($"\nLoaded linetype from acad.lin: {lineTypeName}");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nWarning: Failed to load linetype {lineTypeName} ({ex.Message}). Skipping linetype change.");
                        lineTypeName = null; // Skip linetype modification
                    }
                }

                int okCount = 0, skipCount = 0;

                foreach (var layerName in layerNames)
                {
                    if (!lt.Has(layerName))
                    {
                        skipCount++;
                        continue;
                    }

                    ObjectId ltrId = lt[layerName];
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(ltrId, OpenMode.ForWrite);

                    // Skip external reference dependent layers
                    if (ltr.IsDependent)
                    {
                        ed.WriteMessage($"\nSkipped Xref-dependent layer: {layerName}");
                        skipCount++;
                        continue;
                    }

                    try
                    {
                        // Change color
                        if (targetAci != null)
                        {
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, targetAci.Value);
                        }

                        // Change linetype
                        if (!string.IsNullOrEmpty(lineTypeName) && ltt.Has(lineTypeName))
                        {
                            ltr.LinetypeObjectId = ltt[lineTypeName];
                        }

                        okCount++;
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nFailed to modify layer {layerName}: {ex.Message}");
                        skipCount++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\nDone: {okCount} layers modified, {skipCount} skipped/failed.");
            }
        }
    }
}
