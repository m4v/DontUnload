using System;
using System.Diagnostics;
using System.Collections.Generic;

using UnityEngine;

namespace DontUnload
{

	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class DontUnload : MonoBehaviour
	{
        Vessel activeVessel;
        List<Vessel> keepLoaded;
        float originalLoadDistance;
        Dictionary<Vessel, float> originalPackDistance;

        float distanceMargin = 500;
        /* The distance at what something can land safely seems to be 10km */
        //float safeLandDistance = 10000;

        int ModuleParachuteID = "ModuleParachute".GetHashCode ();

        Stopwatch _SW = new Stopwatch ();
        int _callCounter = 0;
        long _time, _ticks;

        void Start ()
        {
            keepLoaded = new List<Vessel> ();
            originalLoadDistance = Vessel.loadDistance;
            originalPackDistance = new Dictionary<Vessel, float> ();
        }

        void OnDestroy ()
        {
            Vessel.loadDistance = originalLoadDistance;
        }

		void Update ()
        {
#if DEBUG
            _callCounter++;
            _SW.Start ();
#endif
            activeVessel = FlightGlobals.ActiveVessel;

            /* active vessel is always keeped */
            /*if (!keepLoaded.Contains (activeVessel)) {
                keepLoaded.Add (activeVessel);
            }*/

            List<Vessel> vessels = getVessels ();
            for (int i = 0; i < vessels.Count; i++) {
                Vessel vessel = vessels [i];
                if (!keepLoaded.Contains (vessel)) {
                    if (vessel.IsControllable || hasParachutes (vessel)) {
                        keepLoaded.Add (vessel);
                    }
                }
            }

            float maxVesselDistance = 0;
            float vesselDistance = 0;
            for (int i = 0; i < keepLoaded.Count; i++) {
                Vessel vessel = keepLoaded [i];
                if (vessel == null) {
                    keepLoaded.RemoveAt (i);
                    i--;
                    continue;
                }
                //if (vessel != activeVessel) {
                    if (!checkSituation (vessel)) {
                        string name = vessel.GetName ();
                        _debug ("Removing {0} from keep list because {1}", name,
                                vessel.situation);
                        if (vessel != activeVessel) {
                            ScreenMessages.PostScreenMessage (String.Format ("{0} {1}.", name,
                                                                             vessel.situation),
                                                              3, ScreenMessageStyle.UPPER_CENTER);
                        }
                        vessel.distancePackThreshold = originalPackDistance[vessel];
                        keepLoaded.RemoveAt (i);
                        i--;
                        continue;
                    }
                //}
                /* calculate largest vessel distance */
                vesselDistance = getDistance (vessel);
                maxVesselDistance = Mathf.Max (maxVesselDistance, vesselDistance);

                /* update distancePackThreshold */
                float packDistance;
                if (!originalPackDistance.TryGetValue(vessel, out packDistance)) {
                    originalPackDistance[vessel] = vessel.distancePackThreshold;
                    packDistance = vessel.distancePackThreshold;
                }
                if (vesselDistance > (packDistance - distanceMargin)) {
                    /* raise */
                    vessel.distancePackThreshold = vesselDistance + distanceMargin;
                } else {
                    /* keep default */
                    vessel.distancePackThreshold = packDistance;
                }
            }

            /* update loadDistance */
            if (maxVesselDistance > (originalLoadDistance - distanceMargin)) {
                /* raise */
                Vessel.loadDistance = maxVesselDistance + distanceMargin;
            } else {
                /* keep default */
                Vessel.loadDistance = originalLoadDistance;
            }

#if DEBUG
            _SW.Stop ();
            if (_callCounter > 100) {
                _time = _SW.ElapsedMilliseconds;
                _ticks = _SW.ElapsedTicks;
                _SW.Reset ();
                _callCounter = 0;
            }
            _debugShowVesselInfo ();
#endif
        }

        bool hasParachutes(Vessel vessel)
        {
            using (List<Part>.Enumerator enm = vessel.Parts.GetEnumerator()) {
                while (enm.MoveNext ()) {
                    Part part = enm.Current;
                    if (part.Modules.Contains(ModuleParachuteID)) {
                        return true;
                    }
                }
            }
            return false;
        }

        float getDistance (Vessel vessel)
        {
            return Vector3.Distance(activeVessel.transform.position, vessel.transform.position);
        }

        float getRelativeSpeed (Vessel vessel)
        {
            return (activeVessel.GetSrfVelocity() - vessel.GetSrfVelocity()).magnitude;
        }

        List<Vessel> getVessels ()
        {
            List<Vessel> list = new List<Vessel> ();
            using (List<Vessel>.Enumerator enm = FlightGlobals.Vessels.GetEnumerator()) {
                while (enm.MoveNext()) {
                    Vessel vessel = enm.Current;
                    if (vessel.loaded && !vessel.packed && checkSituation(vessel)) {
                        list.Add (vessel);
                    }
                }
            }
            return list;
        }

        bool checkSituation (Vessel vessel)
        {
            switch (vessel.situation) {
            case Vessel.Situations.FLYING:
                return true;
            default:
                return false;
            } 
        }

        /* Debug methods */
        Dictionary<Vessel, float> _terrain = new Dictionary<Vessel, float>();

        [Conditional("DEBUG")]
        void _debug (string s, params object[] values)
        {
            print (String.Format (s, values));
        }

        [Conditional("DEBUG")]
        void _debugShowVesselInfo ()
        {
            List<Vessel> vessels = FlightGlobals.Vessels;
            List<string> debugInfo = new List<string>();
            debugInfo.Add(String.Format ("time {0}/{1:F4} ticks {2}/{3:F4}",
                                         _time, (float)_time/100,
                                         _ticks, (float)_ticks/100));
            string name;
            for (int i = 0; i < vessels.Count; i++) {
                Vessel vessel = vessels [i];
                if (!vessel.loaded) {
                    continue;
                }
                if (keepLoaded.Contains(vessel)) {
                    name = String.Format("<b><color=red>{0}</color>{1}</b>", vessel.GetName(),
                                         vessel == activeVessel ? " (active)" : "");
                } else {
                    name = vessel.GetName();
                }
                float distance = getDistance(vessel);
                /* this is for figure more or less at what distance the ground gets removed */
                float terrain = 0f;
                if (vessel != activeVessel) {
                    if (vessel.GetHeightFromTerrain() == -1) {
                        if (!_terrain.TryGetValue(vessel, out terrain)) {
                            _terrain[vessel] = distance;
                            terrain = distance;
                        }
                    } else {
                        _terrain.Remove (vessel);
                    }
                }
                debugInfo.Add (String.Format ("{0}\n{1}\n" +
                                              "dis: {4:F2} rel spd: {9:F2}\n" +
                                              "ldis: {5:F2} pdis: {7:F2}\n" + 
                                              "alt: {2:F2} spd: {3:F2}\n" +
                                              "srf alt: {8:F2} ({10})\n" +
                                              "packed: {6}",
                                              name,
                                              vessel.situation,
                                              vessel.altitude,
                                              vessel.GetSrfVelocity ().magnitude,
                                              distance,
                                              Vessel.loadDistance,
                                              vessel.packed,
                                              vessel.distancePackThreshold,
                                              vessel.GetHeightFromTerrain(),
                                              getRelativeSpeed(vessel),
                                              terrain));
            }
            if (guiText == null) {
                gameObject.AddComponent<GUIText> ();
                guiText.transform.position = new Vector3 (0.82f, 0.95f, 0f);
                guiText.richText = true;
            }
            guiText.text = String.Join ("\n\n", debugInfo.ToArray());
        }
	}
}

