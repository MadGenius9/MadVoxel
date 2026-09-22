using System.IO;
using MadVoxel.Content;
using MadVoxel.Save;
using MadVoxel.World.Voxel;
using UnityEngine;

namespace MadVoxel.Headless
{
    /// <summary>
    /// The chunk file round-trip. "Quit and reload into the same base" is the Phase 0
    /// success criterion, and this is the layer it rests on.
    /// </summary>
    public static class SaveTests
    {
        public static void Run(ContentDatabase db)
        {
            var world = "HeadlessTest_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                RoundTrip(db, world);
                Palette(db, world);
                UnknownBlocks(db, world);
                Paths(world);
            }
            finally
            {
                var dir = SavePaths.WorldDirectory(world);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        static ushort[] MakeChunk(ContentDatabase db, int seed)
        {
            var blocks = new ushort[Chunk.Volume];
            var stone = db.blocks.IdOf(BlockIds.Stone);
            var dirt = db.blocks.IdOf(BlockIds.Dirt);
            var plank = db.blocks.IdOf(BlockIds.Planks);

            // Long runs plus scattered singles, so both halves of the RLE get exercised.
            for (int i = 0; i < blocks.Length; i++)
            {
                if (i < 2048) blocks[i] = stone;
                else if (i < 3000) blocks[i] = dirt;
                else blocks[i] = 0;
            }
            blocks[0] = plank;
            blocks[2047] = plank;
            blocks[4095] = plank;
            blocks[(seed % 2000) + 100] = plank;
            return blocks;
        }

        static void RoundTrip(ContentDatabase db, string world)
        {
            Harness.Section("save: chunk file round-trip");

            var store = new ChunkFileStore(world, db.blocks);
            var coord = new ChunkCoord(3, 5, -7);
            var written = MakeChunk(db, 1);

            var missing = new ushort[Chunk.Volume];
            Harness.Equal(store.TryLoad(coord, missing), false, "an unedited chunk reports nothing on disk");

            store.Save(coord, written);
            Harness.Check(File.Exists(SavePaths.ChunkFile(world, 3, 5, -7)), "the chunk file was created");

            var read = new ushort[Chunk.Volume];
            Harness.Equal(store.TryLoad(coord, read), true, "the chunk loads back");

            bool identical = true;
            int firstDiff = -1;
            for (int i = 0; i < Chunk.Volume; i++)
            {
                if (read[i] != written[i]) { identical = false; firstDiff = i; break; }
            }
            Harness.Check(identical, "every one of the 4096 voxels survives the round-trip"
                + (identical ? "" : string.Format(" (first difference at {0})", firstDiff)));

            // Negative coordinates are where naive filename parsing falls over.
            var negative = new ChunkCoord(-4, 0, -12);
            var negBlocks = MakeChunk(db, 2);
            store.Save(negative, negBlocks);

            var negRead = new ushort[Chunk.Volume];
            Harness.Equal(store.TryLoad(negative, negRead), true, "a chunk at negative coordinates loads back");

            bool negOk = true;
            for (int i = 0; i < Chunk.Volume; i++) if (negRead[i] != negBlocks[i]) negOk = false;
            Harness.Check(negOk, "negative-coordinate chunks round-trip intact");

            // A fresh store must find chunks written by a previous session.
            var reopened = new ChunkFileStore(world, db.blocks);
            var reread = new ushort[Chunk.Volume];
            Harness.Equal(reopened.TryLoad(coord, reread), true, "a reopened store finds chunks from a previous session");

            bool rereadOk = true;
            for (int i = 0; i < Chunk.Volume; i++) if (reread[i] != written[i]) rereadOk = false;
            Harness.Check(rereadOk, "and reads them back byte for byte");

            // Overwriting must replace, not append.
            var replacement = MakeChunk(db, 3);
            store.Save(coord, replacement);
            var afterOverwrite = new ushort[Chunk.Volume];
            store.TryLoad(coord, afterOverwrite);

            bool overwritten = true;
            for (int i = 0; i < Chunk.Volume; i++) if (afterOverwrite[i] != replacement[i]) overwritten = false;
            Harness.Check(overwritten, "re-saving a chunk replaces the previous contents");
        }

        static void Palette(ContentDatabase db, string world)
        {
            Harness.Section("save: run-length encoding");

            var store = new ChunkFileStore(world, db.blocks);

            // A uniform chunk is the best case: one palette entry, one run.
            var uniform = new ushort[Chunk.Volume];
            var stone = db.blocks.IdOf(BlockIds.Stone);
            for (int i = 0; i < uniform.Length; i++) uniform[i] = stone;

            var coord = new ChunkCoord(99, 1, 99);
            store.Save(coord, uniform);

            var info = new FileInfo(SavePaths.ChunkFile(world, 99, 1, 99));
            Harness.Check(info.Length < 200, string.Format("a uniform chunk compresses to {0} bytes", info.Length));

            var read = new ushort[Chunk.Volume];
            store.TryLoad(coord, read);
            bool ok = true;
            for (int i = 0; i < Chunk.Volume; i++) if (read[i] != stone) ok = false;
            Harness.Check(ok, "the uniform chunk decodes back to solid stone");

            // Worst case: alternating blocks, no runs to exploit. It must still be correct.
            var noisy = new ushort[Chunk.Volume];
            var dirt = db.blocks.IdOf(BlockIds.Dirt);
            for (int i = 0; i < noisy.Length; i++) noisy[i] = (i % 2 == 0) ? stone : dirt;

            var noisyCoord = new ChunkCoord(98, 1, 98);
            store.Save(noisyCoord, noisy);

            var noisyRead = new ushort[Chunk.Volume];
            store.TryLoad(noisyCoord, noisyRead);
            bool noisyOk = true;
            for (int i = 0; i < Chunk.Volume; i++) if (noisyRead[i] != noisy[i]) noisyOk = false;
            Harness.Check(noisyOk, "a chunk with no exploitable runs still round-trips");
        }

        static void UnknownBlocks(ContentDatabase db, string world)
        {
            Harness.Section("save: forward compatibility");

            // Write with the real registry, then read with a registry that has lost a
            // block. The chunk must degrade to air rather than shifting every other id.
            var store = new ChunkFileStore(world, db.blocks);
            var coord = new ChunkCoord(11, 2, 11);

            var blocks = new ushort[Chunk.Volume];
            var stone = db.blocks.IdOf(BlockIds.Stone);
            var glass = db.blocks.IdOf(BlockIds.Glass);
            for (int i = 0; i < blocks.Length; i++) blocks[i] = (i < 2048) ? stone : glass;
            store.Save(coord, blocks);

            var trimmed = ScriptableObject.CreateInstance<BlockRegistry>();
            for (int i = 0; i < db.blocks.blocks.Count; i++)
            {
                if (db.blocks.blocks[i].stringId == BlockIds.Glass) continue; // pretend it was removed
                trimmed.blocks.Add(db.blocks.blocks[i]);
            }
            trimmed.Build();

            var trimmedStore = new ChunkFileStore(world, trimmed);
            var read = new ushort[Chunk.Volume];
            Harness.Equal(trimmedStore.TryLoad(coord, read), true, "a chunk with a removed block still loads");

            int stillStone = 0, becameAir = 0;
            for (int i = 0; i < Chunk.Volume; i++)
            {
                if (i < 2048 && read[i] == trimmed.IdOf(BlockIds.Stone)) stillStone++;
                if (i >= 2048 && read[i] == 0) becameAir++;
            }
            Harness.Equal(stillStone, 2048, "surviving blocks keep their identity");
            Harness.Equal(becameAir, 2048, "removed blocks degrade to air, not to the wrong block");

            // Restore the real registry's runtime ids for any later test.
            db.blocks.Build();
        }

        static void Paths(string world)
        {
            Harness.Section("save: paths");

            Harness.Equal(SavePaths.Sanitise("My World"), "My World", "ordinary names pass through");
            Harness.Equal(SavePaths.Sanitise(""), "World", "an empty name falls back");
            Harness.Check(!SavePaths.Sanitise("bad/name\\here").Contains("/"), "path separators are stripped from world names");

            Harness.Check(SavePaths.ChunkFile(world, -1, 0, 2).EndsWith("c.-1.0.2.mvc"), "chunk filenames encode negative coordinates");
            Harness.Check(SavePaths.WorldExists("definitely not a world " + System.Guid.NewGuid()) == false,
                "a missing world reports as missing");
        }
    }
}
