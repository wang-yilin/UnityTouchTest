using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization; // for converting strings to floats


public class TouchTest : MonoBehaviour
{
    private string report = "";
    public GUIStyle style;

    private Vector2 touches;
    private Vector2 touchesRaw;
    private Vector2 touchesLast;

    // Stores history of toucheration values, subjected to low-pass filter
    List<TouchDataPoint> touch_buffer;

    public const float ALPHA = 0.15f; // low-pass alpha
    public const float LOOKBACK = -1.5f; // touch memory length in seconds
    public const float LOCKOUT = 1.0f; // prevents double-recognition of pattern
    public const float MATCH_THRESHOLD = 1200.0f; // lower = more strict // changed for DTW

    public const int SAKOE_CHIBA_BAND = 6; // maximum distance allowed deviated from the diagonal path of the DTW matrix
    public float timeSinceDetected = 0.0f; // time elapsed since the last time a pattern was detected
    public const float DTW_RESULT_DURATION = 2.0f; // the time duration to show DTW result on screen 

    Dictionary<string, List<TouchDataPoint>> patternDict = new Dictionary<string, List<TouchDataPoint>>(); // a dictionary that stores the pattern and its name

    TouchDataPoint touch_smoothed;

    public class TouchDataPoint
    {
        //class for storing an toucheration value and its time of measurement

        public float t { get; set; } // stores time relative to current time 
        public float x { get; set; }
        public float y { get; set; }
        public float t_abs { get; set; } // time since program start on creation

        public TouchDataPoint()
        {
            t = 0f;
            x = 0f;
            y = 0f;
            t_abs = Time.realtimeSinceStartup;
        }

        // TouchDataPoint constructor that takes five floats
        public TouchDataPoint(float t, float x, float y)
        {
            this.t = 0f;
            this.x = x;
            this.y = y;
            this.t_abs = t;
        }

        // TouchDataPoint constructor that takes one float and a Vector2
        public TouchDataPoint(float t, Vector2 vec)
        {
            this.t = 0;
            x = vec.x;
            y = vec.y;
            t_abs = t;
        }

        // TouchDataPoint constructor that takes a string, for importing from txt file
        public TouchDataPoint(string s)
        {
            // mainly useful for reading from txt file
            string[] tokens = s.Split(',');
            if (tokens.Length != 3)
            {
                Debug.LogError("Error parsing TouchDataPoint from string.");
                return;
            }
            float _t = 0f;
            float _x = 0f;
            float _y = 0f;
            if (float.TryParse(tokens[0], out _t) &&
                float.TryParse(tokens[1], out _x) &&
                float.TryParse(tokens[2], out _y))
            {
                t = _t;
                x = _x;
                y = _y;
            }
            else { Debug.LogError("Error parsing TouchDataPoint from string (" + s + ")"); }
        }

        // TouchDataPoint constructor that copies from another TouchDataPoint
        public TouchDataPoint(TouchDataPoint other)
        {
            this.t = other.t;
            this.x = other.x;
            this.y = other.y;
            this.t_abs = other.t_abs;
        }

        // returns x and y as a Vector2
        public Vector2 toVector()
        {
            return new Vector2(x, y);
        }

        // returns the TouchDataPoint in a string format
        string toString()
        {
            return t + "," + x + "," + y;
        }
    }

    public class Pattern
    {
        public string name;
        public List<TouchDataPoint> data;
        public List<List<TouchDataPoint>> trials;
        public float lastRecognition;

        // Pattern constructor that takes a string and a List of TouchDataPoints
        public Pattern(string name, ICollection<TouchDataPoint> points)
        {
            this.name = name;
            this.trials = new List<List<TouchDataPoint>>();
            trials.Add(points.ToList());
            this.lastRecognition = 0f;
            CalculateData();
        }

        // Pattern constructor that takes a string and constructs a new Pattern
        public Pattern(string name)
        {
            this.name = name;
            this.lastRecognition = 0f;
            this.trials = new List<List<TouchDataPoint>>();
            data = new List<TouchDataPoint>();
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

            data = new List<TouchDataPoint>(finalSize);

            for (int i = 0; i < finalSize; i++)
            {
                Vector2 vector = new Vector2(0, 0);
                for (int j = 0; j < trials.Count; j++)
                {
                    vector += trials.ElementAt(j).ElementAt(i + startIndex - offsets[j]).toVector();
                }
                vector = vector / trials.Count;
                data.Add(new TouchDataPoint(trials[0].ElementAt(i + startIndex - offsets[0]).t_abs, vector));
            }
        }

        private int maximizeXcorr(List<TouchDataPoint> l1, List<TouchDataPoint> l2)
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

            using (StreamWriter sw = new StreamWriter("xcorr.csv"))
            {
                sw.WriteLine("l1");
                sw.WriteLine(",x,y");
                foreach (var ADP in l1)
                {
                    sw.WriteLine(ADP.t + "," + ADP.x + "," + ADP.y);
                }
                sw.WriteLine("l2");
                sw.WriteLine(",x,y");
                foreach (var ADP in l2)
                {
                    sw.WriteLine(ADP.t + "," + ADP.x + "," + ADP.y);
                }
                sw.WriteLine("offset: " + bestOffset);
            }


            return bestOffset;
        }

        private float xcorr(List<TouchDataPoint> l1, List<TouchDataPoint> l2)
        {
            float sum = 0f;
            for (int i = 0; i < l1.Count; i++)
            {
                sum += l1.ElementAt(i).x * l2.ElementAt(i).x;
                sum += l1.ElementAt(i).y * l2.ElementAt(i).y;
            }
            return sum;
        }
    }

    Transform phoneTransform;
    bool recording;
    bool trial;
    List<TouchDataPoint> record;
    List<Pattern> patterns;
    int patternIndex;
    string lastPattern = "noTouch";
    string currentPattern = "noTouch";

    void Start()
    {
        recording = false;
        trial = false;

        phoneTransform = gameObject.GetComponent<Transform>();

        touch_buffer = new List<TouchDataPoint>();
        record = new List<TouchDataPoint>();
        patterns = new List<Pattern>();
        patternIndex = 0;

        importPattern("noTouch.csv", "no touch");
        importPattern("tickle.csv", "tickle");
        importPattern("brush.csv", "brush");
    }


    void Update()
    {

        float timeSinceStart;

        if (recording)
        {
            if (Input.touches.Length > 1)
            {
                touchesRaw = new Vector2(Input.touches[1].position[0], Input.touches[1].position[1]);
            }
            else
            {
                touchesRaw = new Vector2(0, 0);
            }
        }
        else
        {
            if (Input.touches.Length == 0)
            {
                touchesRaw = new Vector2(0, 0);
            }
            else
            {
                touchesRaw = new Vector2(Input.touches[0].position[0], Input.touches[0].position[1]);
            }
        }

        touches = touchesRaw - touchesLast; // the displacement between the current and the last touch coordinates
        touchesLast = touchesRaw;

        report = "Coordinates: " + touches + "\ntouch count: " 
            + Input.touchCount + "\npressure support" + Input.touchPressureSupported + "\n";

        // Initialize low pass to first point
        if (touch_smoothed == null)
        {
            timeSinceStart = Time.realtimeSinceStartup;
            touch_smoothed = new TouchDataPoint(timeSinceStart, touches);
            touch_buffer.Add(new TouchDataPoint(touch_smoothed));
        }

        // Low pass filter to attenuate jitter. If we were double-integrating,
        // this would cause drift, but we're not.
        timeSinceStart = Time.realtimeSinceStartup;
        touch_smoothed = new TouchDataPoint(timeSinceStart,
                                            ALPHA * touches + (1 - ALPHA) *
                                            touch_smoothed.toVector());

        // Add this toucheration to our history buffer
        touch_buffer.Add(new TouchDataPoint(touch_smoothed));


        // Loop over history buffer, update time offsets of past touch values
        int i = 0;
        int n = touch_buffer.Count;

        while (i < n - 1)
        {
            touch_buffer[i].t -= Time.deltaTime;
            if (touch_buffer[i].t < LOOKBACK)
            {
                touch_buffer.RemoveAt(i);
                i--;
                n--;
            }
            i++;
        }

        if (recording)
        {
            record.Add(new TouchDataPoint(touch_smoothed));
            report += "\nrecording...";
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

            float minDTW = float.PositiveInfinity;
            string matchedPattern = "stationary"; // the key to patternDict

            //iterate through patternDict to get the best-matched pattern for the current data in touch_buffer
            foreach (KeyValuePair<string, List<TouchDataPoint>> item in patternDict)
            {
                float tempDTW = getDTW(touch_buffer, item.Value);
                if (tempDTW < minDTW)
                {
                    minDTW = tempDTW;
                    matchedPattern = item.Key;
                }
            }

            if (minDTW < MATCH_THRESHOLD)
            {
                if (timeSinceDetected < DTW_RESULT_DURATION)
                {
                    report += "\n" + matchedPattern + "\nDTWscore:" + minDTW;
                    //pattern.lastRecognition = Time.realtimeSinceStartup;
                    timeSinceDetected += Time.deltaTime;
                }
                else 
                {
                    timeSinceDetected = 0;
                    timeSinceStart = 0; // CHECK REDUNDANCY
                }
            }
            else
            {
                report += "\npattern not recognized";
                timeSinceStart = 0; // CHECK REDUNDANCY
            }
        }
    }


    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 100, 20), report, style);
    }

    void updateAllTimes(ICollection<TouchDataPoint> points, float t_curr)
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

    float getDTW(ICollection<TouchDataPoint> sample, ICollection<TouchDataPoint> targets)
    {
        List<float> samplex = new List<float>();
        List<float> sampley = new List<float>();
        List<float> targetx = new List<float>();
        List<float> targety = new List<float>();

        foreach (var dp in sample)
        {
            samplex.Add(dp.x);
            sampley.Add(dp.y);
        }
        foreach (var dp in targets)
        {
            targetx.Add(dp.x);
            targety.Add(dp.y);
        }

        int sampleSize = sample.Count;
        int targetSize = targets.Count;
        float[,] DTWmatrix = new float[sampleSize, targetSize];

        //initialize starting point
        DTWmatrix[0, 0] = euclideanDistance(samplex.ElementAt(0), sampley.ElementAt(0), 
                                            targetx.ElementAt(0), targety.ElementAt(0));

        //initialize first column (horizontal)
        float tempx = targetx.ElementAt(0);
        float tempy = targety.ElementAt(0);
        for (int i = 1; i < sampleSize; i++)
        {
            if (i < SAKOE_CHIBA_BAND)
            {
                DTWmatrix[i, 0] = euclideanDistance(samplex.ElementAt(i), sampley.ElementAt(i), tempx, tempy) + DTWmatrix[i - 1, 0];
            }
            else { DTWmatrix[i, 0] = float.PositiveInfinity; }
        }

        //initialize first row (vertical)
        tempx = samplex.ElementAt(0);
        tempy = sampley.ElementAt(0);
        for (int j = 1; j < targetSize; j++)
        {
            if (j < SAKOE_CHIBA_BAND)
            {
                DTWmatrix[0, j] = euclideanDistance(tempx, tempy, targetx.ElementAt(j), targety.ElementAt(j)) + DTWmatrix[0, j - 1];
            }
            else { DTWmatrix[0, j] = float.PositiveInfinity; }
        }

        //count the rest
        //i - vertical, j - horizontal
        float minValue;
        for (int j = 1; j < targetSize; j++)
        {
            tempx = targetx.ElementAt(j);
            tempy = targety.ElementAt(j);
            for (int i = 1; i < sampleSize; i++)
            {
                if (DTWmatrix[i, j - 1] < DTWmatrix[i - 1, j - 1]) { minValue = DTWmatrix[i, j - 1]; }
                else { minValue = DTWmatrix[i - 1, j - 1]; }
                if (minValue > DTWmatrix[i - 1, j]) { minValue = DTWmatrix[i - 1, j]; }
                DTWmatrix[i, j] = euclideanDistance(samplex.ElementAt(i), sampley.ElementAt(i), tempx, tempy) + minValue;
            }
        }

        return DTWmatrix[sampleSize-1, targetSize-1];
    }

    float euclideanDistance(float x1, float y1, float x2, float y2)
    {
        return (float)Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
    }

    void startRecordingNew () {
        if (!recording)
        {
            trial = false;
            recording = true;
            record = new List<TouchDataPoint>();
        }
    }

    void startRecordingTrial()
    {
        if (!recording)
        {
            trial = true;
            recording = true;
            record = new List<TouchDataPoint>();
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
                newPattern.trials.Add(new List<TouchDataPoint>());
                foreach (var ADP in record)
                {
                    newPattern.trials[0].Add(new TouchDataPoint(ADP));
                }
                newPattern.CalculateData();

                updateAllTimes(newPattern.trials[0], newPattern.trials[0].Last().t_abs);
                updateAllTimes(newPattern.data, newPattern.data.Last().t_abs);

                patterns.Add(newPattern);

                writeToFile(newPattern.trials.Last(), "pattern" + patternIndex + "_0.csv");
                writeToFile(newPattern.data, "pattern" + patternIndex + "_0d.csv");
            }
            else
            {
                patterns[patternIndex - 1].trials.Add(new List<TouchDataPoint>());

                for (int i = 0; i < record.Count; i++)
                {
                    patterns[patternIndex - 1].trials.Last().Add(new TouchDataPoint(record.ElementAt(i)));
                }

                patterns[patternIndex - 1].CalculateData();
                updateAllTimes(patterns[patternIndex-1].data, patterns[patternIndex-1].data.Last().t_abs);

                writeToFile(patterns[patternIndex - 1].trials.Last(), "pattern" + patternIndex + "_" + (patterns[patternIndex - 1].trials.Count - 1) + ".csv");
                writeToFile(patterns[patternIndex - 1].data, "pattern" + patternIndex + "_" + (patterns[patternIndex - 1].trials.Count - 1) + "d.csv");
            }
        }
    }

    void deletePatterns() {
        patterns = new List<Pattern>();
        patternIndex = 0;
    }

    void writeToFile(ICollection<TouchDataPoint> points, string filename) {
        using (StreamWriter sw = new StreamWriter(filename))
        {
            //sw.WriteLine(",x,y,z"); // commented out for better importing
            foreach (var ADP in points)
            {
                sw.WriteLine(ADP.t + "," + ADP.x + "," + ADP.y);
            }
        }
    }

    // import the pattern data in fileName to patternDict with key patternName
    void importPattern(string fileName, string patternName)
    {
        using (FileStream fp = File.OpenRead(fileName))
        {
            using (TextReader reader = new StreamReader(fp))
            {
                string line;
                List<TouchDataPoint> tempList = new List<TouchDataPoint>();
                while ((line = reader.ReadLine()) != null)
                {
                    string[] strs = line.Split(',');
                    // assume the input file is in the format of [t, x, y]
                    float tempT = float.Parse(strs[0], CultureInfo.InvariantCulture.NumberFormat);
                    float tempx = float.Parse(strs[1], CultureInfo.InvariantCulture.NumberFormat);
                    float tempy = float.Parse(strs[2], CultureInfo.InvariantCulture.NumberFormat);
                    tempList.Add(new TouchDataPoint(tempT, tempx, tempy));
                }
                patternDict.Add(patternName, tempList);
            }
        }
    }
}