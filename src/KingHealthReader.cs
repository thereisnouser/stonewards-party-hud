using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace StonewardsPartyHUD
{
    // Discover the game's King health source at runtime rather than hardcoding
    // an unverified class from a particular Stonewards release. Only native
    // game components near a King object are eligible; unrelated players and
    // enemies must never become the displayed King health.
    internal sealed class KingHealthReader
    {
        private static readonly BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly string[] CurrentNames =
        {
            "NetworkcurrentHealth", "CurrentHealth", "currentHealth",
            "GetCurrentHealth", "Health", "health", "HP"
        };

        private static readonly string[] MaximumNames =
        {
            "NetworkcurrentMaxHealth", "CurrentMaxHealth",
            "GetCurrentMaxHealth", "MaxHealth", "maxHealth",
            "GetMaxHealth", "MaximumHealth", "maxHP", "MaxHP"
        };

        private MonoBehaviour _source;
        private MemberInfo _currentMember;
        private MemberInfo _maximumMember;
        private float _nextLookup;

        public bool TryRead(out float current, out float maximum)
        {
            current = 0f;
            maximum = 0f;
            if (_source == null || !_source.isActiveAndEnabled ||
                !_source.gameObject.activeInHierarchy)
                Clear();

            if (_source == null && Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + 2f;
                Locate();
            }
            if (_source == null)
                return false;

            try
            {
                if (TryReadNumber(_source, _currentMember, out current) &&
                    TryReadNumber(_source, _maximumMember, out maximum) &&
                    IsFinite(current) && IsFinite(maximum) && maximum > 0f)
                {
                    current = Mathf.Clamp(current, 0f, maximum);
                    return true;
                }
            }
            catch
            {
                // Reacquire after a destroyed component or a game update.
            }

            Clear();
            return false;
        }

        private void Locate()
        {
            // A missing/renamed King health component is not an error and
            // must not prevent the ordinary party HUD from rendering.
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsSortMode.None))
            {
                if (component == null || !component.isActiveAndEnabled ||
                    !component.gameObject.activeInHierarchy ||
                    component.GetType().Assembly != typeof(GameManager).Assembly ||
                    !IsKingRelated(component))
                    continue;

                try
                {
                    Type type = component.GetType();
                    MemberInfo health = FindNumberMember(type, CurrentNames);
                    MemberInfo max = FindNumberMember(type, MaximumNames);
                    if (health == null || max == null)
                        continue;
                    float current, maximum;
                    if (!TryReadNumber(component, health, out current) ||
                        !TryReadNumber(component, max, out maximum) ||
                        !IsFinite(current) || !IsFinite(maximum) ||
                        maximum <= 0f)
                        continue;

                    _source = component;
                    _currentMember = health;
                    _maximumMember = max;
                    return;
                }
                catch
                {
                    // Other components are still candidates.
                }
            }
        }

        private static bool IsKingRelated(MonoBehaviour component)
        {
            if (HasKingToken(component.GetType().Name))
                return true;

            Transform node = component.transform;
            for (int level = 0; node != null && level < 5; level++, node = node.parent)
                if (HasKingToken(node.name))
                    return true;

            return false;
        }

        private static bool HasKingToken(string name)
        {
            // Avoid accepting "Viking" characters as the King's health.
            if (string.IsNullOrEmpty(name) ||
                name.IndexOf("Viking", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return name.IndexOf("King", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MemberInfo FindNumberMember(Type type, string[] names)
        {
            // Traverse base types to include private fields declared on
            // inherited health components. Exact-case lookup avoids ambiguous
            // matches between similarly named generated Mirror accessors.
            foreach (string name in names)
            {
                for (Type current = type; current != null;
                     current = current.BaseType)
                {
                    var flags = MemberFlags | BindingFlags.DeclaredOnly;
                    PropertyInfo property = current.GetProperty(name, flags);
                    if (property != null && property.GetIndexParameters().Length == 0)
                        return property;

                    FieldInfo field = current.GetField(name, flags);
                    if (field != null)
                        return field;

                    MethodInfo method = current.GetMethod(name, flags, null,
                        Type.EmptyTypes, null);
                    if (method != null && method.ReturnType != typeof(void))
                        return method;
                }
            }
            return null;
        }

        private static bool TryReadNumber(object source, MemberInfo member, out float value)
        {
            value = 0f;
            object raw;
            if (member is PropertyInfo property)
                raw = property.GetValue(source, null);
            else if (member is FieldInfo field)
                raw = field.GetValue(source);
            else if (member is MethodInfo method)
                raw = method.Invoke(source, null);
            else
                return false;

            if (raw == null || !(raw is IConvertible))
                return false;

            value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void Clear()
        {
            _source = null;
            _currentMember = null;
            _maximumMember = null;
        }
    }
}
