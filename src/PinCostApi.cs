using System;
using SuperFantasyKingdom;
using UnityEngine;
using UnityEngine.UI;

namespace PinABuildCost
{
    public static class PinCostApi
    {
        /// <summary>Add or update a pin row for a mod-owned building offer on the Unity main thread.</summary>
        /// <param name="card">The card in the list. The pin row is inserted immediately after it.</param>
        /// <param name="purchase">Its purchase button, including a currently disabled button.</param>
        /// <param name="source">A stable target that survives closing and reopening the card.</param>
        /// <param name="operation">A stable, mod-qualified identifier for the exact action and tier.</param>
        /// <param name="title">Player-facing target and tier name.</param>
        /// <param name="readCost">Read the current purchase price. Return null for an unknown price.</param>
        /// <param name="isComplete">Read authoritative completion state for this target and action.</param>
        /// <param name="canPurchase">Optional read-only check for additional requirements.</param>
        /// <param name="readStock">Optional available-stock getter for offers using a separate inventory.</param>
        public static bool Attach(GameObject card, Button purchase, Component source, string operation,
            string title, Func<ResourceAmount[]> readCost, Func<bool> isComplete, Func<bool> canPurchase = null,
            Func<ResourceType, int> readStock = null) =>
            Attach(card, purchase, source, operation, title, readCost, isComplete, canPurchase, readStock,
                honorFreeBuilding: readStock == null);

        /// <summary>Attach an offer with an explicit policy for the game's free-building mode.
        /// A private inventory must cover the combined price unless its cost is waived.</summary>
        public static bool Attach(GameObject card, Button purchase, Component source, string operation,
            string title, Func<ResourceAmount[]> readCost, Func<bool> isComplete, Func<bool> canPurchase,
            Func<ResourceType, int> readStock, bool honorFreeBuilding)
        {
            if (PinPlugin.Instance == null || card == null || purchase == null || source == null ||
                string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(title) ||
                readCost == null || isComplete == null) return false;
            var target = new PinTarget(source, title, readCost,
                permitted: cost => (canPurchase == null || canPurchase()) &&
                    (readStock != null || ResourceManager.Instance.CanBeBuilt(cost)),
                complete: isComplete, operation: "mod:" + operation, readStock: readStock,
                honorFreeBuilding: honorFreeBuilding);
            PinRow.Create(card, purchase, target);
            return true;
        }
    }
}
