using Sandbox.Game.Entities;
using VRageMath;

namespace HostileTakeover.Models
{
    public class HighlightedBlock
    {
        public long TargetPlayer;
        public Color Color;
        public MyCubeBlock Block;

        public override bool Equals(object obj)
        {
            var comp = obj as HighlightedBlock;
            return comp != null && Equals(comp);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = TargetPlayer.GetHashCode();
                hashCode = (hashCode * 397) ^ Color.GetHashCode();
                hashCode = (hashCode * 397) ^ (Block != null ? Block.GetHashCode() : 0);
                return hashCode;
            }
        }

        protected bool Equals(HighlightedBlock other)
        {
            return TargetPlayer == other.TargetPlayer && Equals(Color, other.Color) && Equals(Block, other.Block);
        }

        public void Reset()
        {
            TargetPlayer = 0;
            Color = Color.Black;
            Block = null;
        }

        public override string ToString()
        {
            return $"[{TargetPlayer:D18}] [{Block.OwnerId:D18}] {Block.DefinitionId?.SubtypeId}";
        }
    }
}