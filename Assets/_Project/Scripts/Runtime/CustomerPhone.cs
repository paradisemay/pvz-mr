using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// The phone a customer holds out at the counter with the barcode of their order (T035, #79). The scanner reads it;
    /// once read, the customer puts it away. Part of the customer (child object), shown only while the code waits.
    /// </summary>
    public sealed class CustomerPhone : MonoBehaviour
    {
        [SerializeField] private GameObject body;
        [SerializeField] private TMP_Text caption;

        /// <summary>The customer holding this phone.</summary>
        public Customer Owner { get; private set; }

        public bool IsShown => body != null && body.activeSelf;

        private void Awake()
        {
            Owner = GetComponentInParent<Customer>();
            if (body != null) body.SetActive(false);
        }

        /// <summary>Holds the phone out towards the player, the screen showing <paramref name="text"/> above the barcode.</summary>
        public void Show(string text)
        {
            if (caption != null) caption.text = text;
            if (body != null) body.SetActive(true);
        }

        /// <summary>Puts the phone away (the code was read, or the customer leaves).</summary>
        public void Hide()
        {
            if (body != null) body.SetActive(false);
        }

        internal void Configure(GameObject phoneBody, TMP_Text phoneCaption)
        {
            body = phoneBody;
            caption = phoneCaption;
        }
    }
}
