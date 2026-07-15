using UnityEngine;

[CreateAssetMenu(
    fileName = "EventChannel_CurrencyChanged",
    menuName = "EventChannel/Currency/Changed")]
public sealed class CurrencyChangedEventChannel
    : EventChannel<CurrencyChangedEventData>
{
}

