using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization; // for converting strings to floats


public class AccelTest : MonoBehaviour
{
    private string report = "";
    public GUIStyle style;

    private Vector3 accel;

    public string character = "";
    public float hue = 1;

    // Stores history of acceleration values, subjected to low-pass filter
    List<AccelDataPoint> accel_buffer;

    public const float ALPHA = 0.15f; // low-pass alpha
    public const float LOOKBACK = -1.5f; // accel memory length in seconds
    public const float LOCKOUT = 1.0f; // prevents double-recognition of pattern
    public const float MATCH_THRESHOLD = 25.0f; // lower = more strict // changed for DTW

    public const int SAKOE_CHIBA_BAND = 3; // maximum distance allowed deviated from the diagonal path of the DTW matrix

    Dictionary<string, List<AccelDataPoint>> patternDict = new Dictionary<string, List<AccelDataPoint>>(); // a dictionary that stores the pattern and its name

    AccelDataPoint accel_smoothed;

    public class AccelDataPoint
    {
        //class for storing an acceleration value and its time of measurement

        public float t { get; set; } // stores time relative to current time 
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float t_abs { get; set; } // time since program start on creation

        public AccelDataPoint()
        {
            t = 0f;
            x = 0f;
            y = 0f;
            z = 0f;
            t_abs = Time.realtimeSinceStartup;
        }

        public AccelDataPoint(float t, float x, float y, float z)
        {
            this.t = 0f;
            this.x = x;
            this.y = y;
            this.z = z;
            this.t_abs = t;
        }

        public AccelDataPoint(float t, Vector3 vec)
        {
            this.t = 0;
            x = vec.x;
            y = vec.y;
            z = vec.z;
            t_abs = t;
        }

        public AccelDataPoint(string s)
        {
            // mainly useful for reading from txt file
            string[] tokens = s.Split(',');
            if (tokens.Length != 4)
            {
                Debug.LogError("Error parsing AccelDataPoint from string.");
                return;
            }
            float _t = 0f;
            float _x = 0f;
            float _y = 0f;
            float _z = 0f;
            if (float.TryParse(tokens[0], out _t) &&
                float.TryParse(tokens[1], out _x) &&
                float.TryParse(tokens[2], out _y) &&
                float.TryParse(tokens[3], out _z))
            {
                t = _t;
                x = _x;
                y = _y;
                z = _z;
            }
            else { Debug.LogError("Error parsing AccelDataPoint from string (" + s + ")"); }
        }

        public AccelDataPoint(AccelDataPoint other)
        {
            this.t = other.t;
            this.x = other.x;
            this.y = other.y;
            this.z = other.z;
            this.t_abs = other.t_abs;
        }

        public Vector3 toVector()
        {
            return new Vector3(x, y, z);
        }

        string toString()
        {
            return t + "," + x + "," + y + "," + z;
        }
    }

    public class Pattern
    {
        public string name;
        public List<AccelDataPoint> data;
        public List<List<AccelDataPoint>> trials;
        public float lastRecognition;

        public Pattern(string name, ICollection<AccelDataPoint> points)
        {
            this.name = name;
            this.trials = new List<List<AccelDataPoint>>();
            trials.Add(points.ToList());
            this.lastRecognition = 0f;
            CalculateData();
        }

        public Pattern(string name)
        {
            this.name = name;
            this.lastRecognition = 0f;
            this.trials = new List<List<AccelDataPoint>>();
            data = new List<AccelDataPoint>();
        }

        public void CalculateData()
        {
            // Aligns trial data by minimizing cross-correlation, then averages
            // across trials to create template

            // Both of below are used for determining length of final list
            List<int> offsets = new List<int>(trials.Count);
            List<int> sizesPlusOffsets = new List<int>(trials.Count);

            for (int i = 0; i < trials.Count; i++) // iterate over trials
            {
                int offset = maximizeXcorr(data, trials.ElementAt(i));
                offsets.Add(offset);
                sizesPlusOffsets.Add(trials.ElementAt(i).Count + offset);
            }

            int startIndex = Mathf.Max(offsets.ToArray());
            int endIndex = Mathf.Min(sizesPlusOffsets.ToArray());

            int finalSize = endIndex - startIndex;

            data = new List<AccelDataPoint>(finalSize);

            for (int i = 0; i < finalSize; i++)
            {
                Vector3 vector = new Vector3(0, 0, 0);
                for (int j = 0; j < trials.Count; j++)
                {
                    vector += trials.ElementAt(j).ElementAt(i + startIndex - offsets[j]).toVector();
                }
                vector = vector / trials.Count;
                data.Add(new AccelDataPoint(trials[0].ElementAt(i + startIndex - offsets[0]).t_abs, vector));
            }
        }

        private int maximizeXcorr(List<AccelDataPoint> l1, List<AccelDataPoint> l2)
        {
            int n1 = l1.Count;
            int n2 = l2.Count;

            float maxXcorr = 0;
            int bestOffset = 0;

            for (int offset = -(n2 - 1); offset <= n1 - 1; offset++)
            {
                float currXcorr = xcorr(l1.GetRange(Mathf.Max(0, offset),
                                                    Mathf.Min(n1, n2 + offset)
                                                    - Mathf.Max(0, offset)),
                                        l2.GetRange(Mathf.Max(0, -offset),
                                                    Mathf.Min(n1, n2 + offset)
                                                    - Mathf.Max(0, offset)));
                if (currXcorr > maxXcorr)
                {
                    maxXcorr = currXcorr;
                    bestOffset = offset;
                }
            }
            /*
            using (StreamWriter sw = new StreamWriter("xcorr.txt"))
            {
                sw.WriteLine("l1");
                sw.WriteLine(",x,y,z");
                foreach (var ADP in l1)
                {
                    sw.WriteLine(ADP.t + "," + ADP.x + "," + ADP.y + "," + ADP.z);
                }
                sw.WriteLine("l2");
                sw.WriteLine(",x,y,z");
                foreach (var ADP in l2)
                {
                    sw.WriteLine(ADP.t + "," + ADP.x + "," + ADP.y + "," + ADP.z);
                }
                sw.WriteLine("offset: " + bestOffset);
            }*/


            return bestOffset;
        }

        private float xcorr(List<AccelDataPoint> l1, List<AccelDataPoint> l2)
        {
            float sum = 0f;
            for (int i = 0; i < l1.Count; i++)
            {
                sum += l1.ElementAt(i).x * l2.ElementAt(i).x;
                sum += l1.ElementAt(i).y * l2.ElementAt(i).y;
                sum += l1.ElementAt(i).z * l2.ElementAt(i).z;
            }
            return sum;
        }

    }

    private Transform phoneTransform;
    bool recording;
    bool trial;
    List<AccelDataPoint> record;
    List<Pattern> patterns;
    int patternIndex;
    public float timeSinceDetected = float.PositiveInfinity; // time elapsed since the last time a pattern was detected
    public const float DTW_RESULT_DURATION = 1.5f; // the time duration to show DTW result on screen
    public string detectedPattern;
    public float detectedDTWscore;

    void Start()
    {
        Input.gyro.enabled = true;

        recording = false;
        trial = false;

        phoneTransform = gameObject.GetComponent<Transform>();

        accel_buffer = new List<AccelDataPoint>();
        record = new List<AccelDataPoint>();
        patterns = new List<Pattern>();
        patternIndex = 0;

        //importPattern("gyroStationary.txt", "gyro stationary");
        //importPattern("gyroTailWag.txt", "gyro tail wag");
        ChangeCharacterPatterns(character);

        /*
        importPattern("roll.txt", "roll");
        importPattern("slither.txt", "slither");
        importPattern("tongueHissing.txt", "tongue hissing");*/
    }






    void Update()
    {
        float timeSinceStart;

        accel = Input.gyro.userAcceleration;

        // Initialize low pass to first point
        if (accel_smoothed == null)
        {
            timeSinceStart = Time.realtimeSinceStartup;
            accel_smoothed = new AccelDataPoint(timeSinceStart, accel);
            accel_buffer.Add(new AccelDataPoint(accel_smoothed));
        }

        // Low pass filter to attenuate jitter. If we were double-integrating,
        // this would cause drift, but we're not.
        timeSinceStart = Time.realtimeSinceStartup;
        accel_smoothed = new AccelDataPoint(timeSinceStart,
                                            ALPHA * accel + (1 - ALPHA) *
                                            accel_smoothed.toVector());

        // Add this acceleration to our history buffer
        accel_buffer.Add(new AccelDataPoint(accel_smoothed));

        // Loop over history buffer, update time offsets of past accel values
        int i = 0;
        int n = accel_buffer.Count;

        while (i < n - 1)
        {
            accel_buffer[i].t -= Time.deltaTime;
            if (accel_buffer[i].t < LOOKBACK)
            {
                accel_buffer.RemoveAt(i);
                i--;
                n--;
            }
            i++;
        }

        if (recording)
        {
            record.Add(new AccelDataPoint(accel_smoothed));
            report = "recording...";
            if (record[0].t_abs - Time.realtimeSinceStartup < LOOKBACK)
            {
                stopRecording();
            }
        }
        else
        {
            if (record.Count > 0)
            {
                updateAllTimes(record, record.Last().t_abs);
            }

            report = "x: " + accel.x + "\ny:" + accel.y + "\nz: " + accel.z + "\n";

            float minDTW = float.PositiveInfinity;
            string matchedPattern = "stationary"; // the key to patternDict

            //iterate through patternDict to get the best-matched pattern for the current data in accel_buffer
            foreach (KeyValuePair<string, List<AccelDataPoint>> item in patternDict)
            {
                float tempDTW = getDTW(accel_buffer, item.Value);
                if (tempDTW < minDTW)
                {
                    minDTW = tempDTW;
                    matchedPattern = item.Key;
                }
            }

            if (timeSinceDetected < DTW_RESULT_DURATION)
            {
                report += detectedPattern + "\nDTW: " + detectedDTWscore;
                timeSinceDetected += Time.deltaTime;
            }
            else
            {
                // < threshold, recognized and not stationary
                if (minDTW < MATCH_THRESHOLD && matchedPattern != "gyroStationary.txt")
                {
                    timeSinceDetected = 0;
                    detectedPattern = matchedPattern;
                    detectedDTWscore = minDTW;
                    //report = matchedPattern + "\nDTWscore:" + minDTW;
                    //pattern.lastRecognition = Time.realtimeSinceStartup;
                    hue = .3f; // green
                }
                // < threshold, recognized and IS stationary
                else if (minDTW < MATCH_THRESHOLD && matchedPattern == "gyroStationary.txt")
                {
                    report += matchedPattern + "\nDTW score: " + minDTW + "\n";
                    hue = 1f; // red

                }
                else
                {
                    //report = "";
                    report += "pattern not recognized";
                    //timeSinceStart = 0; // CHECK REDUNDANCY
                    hue = 1f; // red
                }
            }
        }


    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 100, 20), report, style);
    }

    void updateAllTimes(ICollection<AccelDataPoint> points, float t_curr)
    {
        // Sets each data point's relative t value
        // using the reference time t_curr.
        // (t_curr typically greater than or equal to t_abs for all points,
        // so t ends up <= 0 for each point)
        foreach (var point in points)
        {
            point.t = point.t_abs - t_curr;
        }
    }

    float getDTW(ICollection<AccelDataPoint> sample, ICollection<AccelDataPoint> targets)
    {
        List<float> sampleX = new List<float>();
        List<float> sampleY = new List<float>();
        List<float> sampleZ = new List<float>();
        List<float> targetX = new List<float>();
        List<float> targetY = new List<float>();
        List<float> targetZ = new List<float>();

        foreach (var dp in sample)
        {
            sampleX.Add(dp.x);
            sampleY.Add(dp.y);
            sampleZ.Add(dp.z);
        }
        foreach (var dp in targets)
        {
            targetX.Add(dp.x);
            targetY.Add(dp.y);
            targetZ.Add(dp.z);
        }

        int sampleSize = sample.Count;
        int targetSize = targets.Count;
        float[,] DTWmatrix = new float[sampleSize, targetSize];

        //initialize starting point
        DTWmatrix[0, 0] = euclideanDistance(sampleX.ElementAt(0), sampleY.ElementAt(0), sampleZ.ElementAt(0),
                                            targetX.ElementAt(0), targetY.ElementAt(0), targetZ.ElementAt(0));

        //initialize first column (horizontal)
        float tempX = targetX.ElementAt(0);
        float tempY = targetY.ElementAt(0);
        float tempZ = targetZ.ElementAt(0);
        for (int i = 1; i < sampleSize; i++)
        {
            if (i < SAKOE_CHIBA_BAND)
            {
                DTWmatrix[i, 0] = euclideanDistance(sampleX.ElementAt(i), sampleY.ElementAt(i), sampleZ.ElementAt(i), tempX, tempY, tempZ) + DTWmatrix[i - 1, 0];
            }
            else { DTWmatrix[i, 0] = float.PositiveInfinity; }
        }

        //initialize first row (vertical)
        tempX = sampleX.ElementAt(0);
        tempY = sampleY.ElementAt(0);
        tempZ = sampleZ.ElementAt(0);
        for (int j = 1; j < targetSize; j++)
        {
            if (j < SAKOE_CHIBA_BAND)
            {
                DTWmatrix[0, j] = euclideanDistance(tempX, tempY, tempZ, targetX.ElementAt(j), targetY.ElementAt(j), targetZ.ElementAt(j)) + DTWmatrix[0, j - 1];
            }
            else { DTWmatrix[0, j] = float.PositiveInfinity; }
        }

        //count the rest
        //i - vertical, j - horizontal
        float minValue;
        for (int j = 1; j < targetSize; j++)
        {
            tempX = targetX.ElementAt(j);
            tempY = targetY.ElementAt(j);
            tempZ = targetZ.ElementAt(j);
            for (int i = 1; i < sampleSize; i++)
            {
                if (DTWmatrix[i, j - 1] < DTWmatrix[i - 1, j - 1]) { minValue = DTWmatrix[i, j - 1]; }
                else { minValue = DTWmatrix[i - 1, j - 1]; }
                if (minValue > DTWmatrix[i - 1, j]) { minValue = DTWmatrix[i - 1, j]; }
                DTWmatrix[i, j] = euclideanDistance(sampleX.ElementAt(i), sampleY.ElementAt(i), sampleZ.ElementAt(i),
                                                    tempX, tempY, tempZ) + minValue;
            }
        }

        return DTWmatrix[sampleSize-1, targetSize-1];
    }

    float euclideanDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        return (float) Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2) + Math.Pow(z1 - z2, 2));
    }

    void startRecordingNew () {
        if (!recording)
        {
            trial = false;
            recording = true;
            record = new List<AccelDataPoint>();
        }
    }

    void startRecordingTrial()
    {
        if (!recording)
        {
            trial = true;
            recording = true;
            record = new List<AccelDataPoint>();
        }
    }

    void stopRecording() {
        if (recording)
        {
            recording = false;

            if (!trial)
            {
                name = "pattern_" + (patternIndex+1);
                patternIndex++;

                Pattern newPattern = new Pattern(name);
                newPattern.trials.Add(new List<AccelDataPoint>());
                foreach (var ADP in record)
                {
                    newPattern.trials[0].Add(new AccelDataPoint(ADP));
                }
                newPattern.CalculateData();

                updateAllTimes(newPattern.trials[0], newPattern.trials[0].Last().t_abs);
                updateAllTimes(newPattern.data, newPattern.data.Last().t_abs);

                patterns.Add(newPattern);

                writeToFile(newPattern.trials.Last(), "pattern" + patternIndex + "_0.txt");
                writeToFile(newPattern.data, "pattern" + patternIndex + "_0d.txt");
            }
            else
            {
                patterns[patternIndex - 1].trials.Add(new List<AccelDataPoint>());

                for (int i = 0; i < record.Count; i++)
                {
                    patterns[patternIndex - 1].trials.Last().Add(new AccelDataPoint(record.ElementAt(i)));
                }

                patterns[patternIndex - 1].CalculateData();
                updateAllTimes(patterns[patternIndex-1].data, patterns[patternIndex-1].data.Last().t_abs);

                writeToFile(patterns[patternIndex - 1].trials.Last(), "pattern" + patternIndex + "_" + (patterns[patternIndex - 1].trials.Count - 1) + ".txt");
                writeToFile(patterns[patternIndex - 1].data, "pattern" + patternIndex + "_" + (patterns[patternIndex - 1].trials.Count - 1) + "d.txt");

            }
        }
    }

    void deletePatterns() {
        patterns = new List<Pattern>();
        patternIndex = 0;
    }

    void writeToFile(ICollection<AccelDataPoint> points, string filename) {
        using (StreamWriter sw = new StreamWriter(filename))
        {
            foreach (var ADP in points)
            {
                sw.WriteLine(ADP.t + "," + ADP.x + "," + ADP.y + "," + ADP.z);
            }
        }
    }

    // import the pattern data in fileName to patternDict with key patternName
    void importPattern(string fileName)
    {
        
        using (FileStream fp = File.OpenRead("Assets/Resources/gestures/"+ fileName))
        {
            using (TextReader reader = new StreamReader(fp))
            {
                string line;
                List<AccelDataPoint> tempList = new List<AccelDataPoint>();
                while ((line = reader.ReadLine()) != null)
                {
                    string[] strs = line.Split(',');
                    // assume the input file is in the format of [t, x, y, z]
                    float tempT = float.Parse(strs[0], CultureInfo.InvariantCulture.NumberFormat);
                    float tempX = float.Parse(strs[1], CultureInfo.InvariantCulture.NumberFormat);
                    float tempY = float.Parse(strs[2], CultureInfo.InvariantCulture.NumberFormat);
                    float tempZ = float.Parse(strs[3], CultureInfo.InvariantCulture.NumberFormat);
                    tempList.Add(new AccelDataPoint(tempT, tempX, tempY, tempZ));
                }
                patternDict.Add(fileName, tempList);
            }
        }
    }

    public void ChangeCharacterPatterns(string _character)
    {
        character = _character;

        // clear the dict
        patternDict.Clear();
        // import default pattern to compare to (in case any gesture's resting
        // DTW score is below the recognition threshold)
        importPattern("gyroStationary.txt");


        switch (character)
        {
            case "baby":
                importPattern("baby-airplane.txt");
                importPattern("baby-bottle.txt");
                importPattern("baby-bounce.txt");
                importPattern("baby-rock.txt");
                break;
            case "anteater":
                importPattern("anteater-dig.txt");
                importPattern("anteater-tongue.txt");
                break;
            case "goldenRetriever":
                importPattern("goldenRetriever-hop.txt");
                importPattern("goldenRetriever-tailWag.txt");
                importPattern("goldenRetriever-throwBall.txt");
                importPattern("goldenRetriever-tugOfWar.txt");
                break;
            case "flamingo":
                importPattern("flamingo-uncurlNeck.txt");
                importPattern("flamingo-fly.txt");
                break;
            case "zebra":
                importPattern("zebra-graze.txt");
                importPattern("zebra-kickDefense.txt");
                importPattern("zebra-tailWag.txt");
                importPattern("zebra-trot.txt");
                break;
            case "lynx":
                importPattern("lynx-claw.txt");
                importPattern("lynx-pounce.txt");
                importPattern("lynx-lick.txt");
                break;
            case "pig":
                importPattern("pig-rootTruffles.txt");
                importPattern("pig-tailWag.txt");
                break;
            case "snake":
                importPattern("snake-slither.txt");
                importPattern("snake-hiss.txt");
                break;
            case "rooster":
                importPattern("rooster-peck.txt");
                importPattern("rooster-tailWag.txt");
                break;
            case "raccoon":
                importPattern("raccoon-tailWag.txt");
                importPattern("raccoon-climbTree.txt");
                importPattern("raccoon-standUp.txt");
                break;
            case "pony":
                importPattern("pony-rearUp.txt");
                importPattern("pony-brushHair.txt");
                importPattern("pony-trot.txt");
                break;
            case "duke":
                importPattern("duke-rollOnBack.txt");
                importPattern("duke-pet.txt");
                importPattern("duke-hop.txt");
                importPattern("duke-tugOfWar.txt");
                importPattern("duke-throwBall.txt");
                importPattern("duke-tailWag.txt");
                break;
            case "giraffe":
                importPattern("giraffe-eatLeaves.txt");
                importPattern("giraffe-trot.txt");
                importPattern("giraffe-longNeck.txt");
                importPattern("giraffe-kick.txt");
                break;
            case "elephant":
                importPattern("elephant-stampede.txt");
                break;
        }
    }
}