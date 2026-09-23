using System.Collections.Generic;
using MadVoxel.Content;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using MadVoxel.World.Terrain;
using UnityEngine;

namespace MadVoxel.Traders
{
    /// <summary>
    /// Stands a counter at every trader outpost the world generator laid down, and owns
    /// their books.
    ///
    /// The outposts have been in the map since Phase 0 - two of them, placed on
    /// farmland and clay hills before anything else could take the ground. Until now
    /// they were buildings with nobody in them.
    /// </summary>
    public class TraderWorld : MonoBehaviour
    {
        /// <summary>How close you have to park to tip a harvest into their intake.</summary>
        public const float TipRange = 14f;

        readonly List<TraderPost> _posts = new List<TraderPost>();

        ContentDatabase _content;
        WorldClock _clock;
        Transform _root;

        public IReadOnlyList<TraderPost> All { get { return _posts; } }

        public void Init(ContentDatabase content, TerrainWorld voxels, WorldClock clock)
        {
            _content = content;
            _clock = clock;

            var rootGo = new GameObject("Traders");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;

            Populate(voxels);
        }

        /// <summary>
        /// One counter per outpost, matched to a trader by the outpost variant its
        /// definition asks for. A definition whose outpost the generator could not
        /// place - a seed with no room for the second one - simply has no counter,
        /// which is better than putting two traders in one stall.
        /// </summary>
        void Populate(TerrainWorld voxels)
        {
            if (voxels == null || voxels.Terrain == null || voxels.Terrain.Pois == null) return;

            var outposts = voxels.Terrain.Pois.TraderOutposts;
            for (int i = 0; i < outposts.Count; i++)
            {
                var definition = DefinitionForVariant(i);
                if (definition == null) continue;

                var poi = outposts[i];

                var go = new GameObject("TraderPost");
                go.transform.SetParent(_root, false);

                // On the pad the generator flattened, a little in from the centre so
                // the stall is not standing in the middle of the courtyard.
                go.transform.position = new Vector3(poi.CentreX + 0.5f, poi.PadY + 1f, poi.CentreZ - 3.5f);

                var post = go.AddComponent<TraderPost>();
                post.Init(definition, _clock);
                _posts.Add(post);
            }
        }

        TraderDefinition DefinitionForVariant(int variant)
        {
            if (_content == null) return null;

            for (int i = 0; i < _content.traders.Count; i++)
            {
                if (_content.traders[i] != null && _content.traders[i].outpostVariant == variant) return _content.traders[i];
            }

            // More outposts than traders: fall back rather than leave a stall empty.
            return variant < _content.traders.Count ? _content.traders[variant] : null;
        }

        public TraderPost Find(string traderId)
        {
            for (int i = 0; i < _posts.Count; i++)
            {
                if (_posts[i] != null && _posts[i].Definition != null
                    && _posts[i].Definition.stringId == traderId) return _posts[i];
            }
            return null;
        }

        /// <summary>The counter you are parked at, for tipping a harvest straight in.</summary>
        public TraderPost NearestPost(Vector3 from, float range)
        {
            TraderPost best = null;
            float bestSqr = range * range;

            for (int i = 0; i < _posts.Count; i++)
            {
                var post = _posts[i];
                if (post == null) continue;

                float sqr = (post.transform.position - from).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = post;
            }
            return best;
        }

        /// <summary>
        /// Sells a hopper of produce over the counter. Returns the litres taken.
        ///
        /// Deliberately refuses anything but a harvester's load: a seed drill's hopper
        /// also holds litres, of seed, and a trader who bought those would turn "buy
        /// seed, tip it straight back" into a laundry. Seed is bought by the item and
        /// stays bought.
        /// </summary>
        public float SellHarvest(TraderPost post, MadVoxel.Inventory.Inventory bag, CropDefinition crop,
                                 float litres, bool fromHarvester, out int paid)
        {
            paid = 0;
            if (post == null || post.State == null || bag == null || crop == null || litres <= 0f) return 0f;

            if (!fromHarvester)
            {
                Notifications.Post("They buy harvested produce, not seed");
                return 0f;
            }

            post.Refresh();

            float taken;
            var result = post.State.BuyLitres(bag, crop, litres, out taken, out paid);

            if (result != TradeResult.Ok)
            {
                Notifications.Post(result == TradeResult.NoRoom
                    ? string.Format("No room for {0} tokens - clear a slot", paid)
                    : TraderState.Describe(result));
                paid = 0;
                return 0f;
            }

            Notifications.PostFormat("Sold {0:0} L of {1} for {2} tokens", taken, crop.displayName, paid);
            return taken;
        }

        // -------------------------------------------------------------------- saving

        public void LoadState(string traderId, int reputation, double lastRestockHours, IList<int> stock)
        {
            var post = Find(traderId);
            if (post == null || post.State == null) return;

            post.State.LoadState(reputation, lastRestockHours, stock);
        }

        public void Clear()
        {
            for (int i = 0; i < _posts.Count; i++)
            {
                if (_posts[i] != null) Destroy(_posts[i].gameObject);
            }
            _posts.Clear();
        }
    }
}
