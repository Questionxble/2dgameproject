using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LogManager : MonoBehaviour
{
    // Singleton instance thats able to be globally accessed
    public static LogManager instance;

    private string logName = string.Empty; // Log Name

    private Stream logStream; // Stream to log file

    // ERROR [0] > WARNING [1] > INFO [2] > DEBUG [3]
    public static int ERROR = 0;
    public static int WARNING = 1;
    public static int INFO = 2;
    public static int DEBUG = 3;

    private String[] logLevels = {"ERROR", "WARNING", "INFO", "DEBUG"};

    // logLevel writes all logs that are the set level or LOWER
    private int fileLevel = INFO;

    // consoleLevel displays all logs that are the set level or LOWER
    private int consoleLevel = DEBUG;

    // maximum size of the log in bytes
    private int maxSize = 500;
    private int offset = 0;

    // Initialize code
    private void Awake()
    {
        // If instance does not exist, set it to this object
        if (instance == null)
            instance = this;

        // If instance does exist and it is not this, destroy this. Only 1 instance can exist
        else if (instance != this)
            Destroy(this.gameObject);

        DontDestroyOnLoad(this.gameObject);

        // Create Log File
        createLog();
    }

    private void createLog()
    {
        // If within valid range, create file log
        if (fileLevel >= 0 && fileLevel < 3)
        {
            // Current Timestamp
            DateTime date = DateTime.UtcNow;
            logName = date.ToString(new CultureInfo("en-US"));

            // Replace slash with dash to prevent OS name conflicts
            logName = logName.Replace("/", "-");
            // Colon with underscore
            logName = logName.Replace(":", "_");

            // Add file extension
            logName += ".txt";

            // Add Logs subfolder
            logName = "logs/" + logName;

            logStream = new FileStream(logName, FileMode.OpenOrCreate);

            // Directly write Log Level into log

            writeToLogFile("Log Level: " + logLevels[fileLevel]);

            // Directly write OS Info into log
            writeToLogFile("Platform: " + Environment.OSVersion.Platform);
            writeToLogFile("OS Version: " + Environment.OSVersion.VersionString);
            writeToLogFile("OS Description: " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);

            // Log that the app started
            log("Application Started!", INFO);
        }
    }

    // dest refers to destination, -1 = both, 0 = only console, 1 = only file
    public void log(string message, int level, int dest = -1, Boolean closeStream=false)
    {
        // Write to console (if applicable)
        if (dest != 1)
        {
            // Only write if the written log level is less than or equal to the set level
            if (level <= consoleLevel){
                if (level == ERROR) {
                    Debug.LogError(message);
                } else if (level == WARNING)
                {
                    Debug.LogWarning(message);
                } else if (level == INFO)
                {
                    Debug.Log("[INFO]  " + message);
                } else if (level == DEBUG)
                {
                    Debug.Log("[DEBUG] " + message);
                }
            }
        }
        // Write to file (if applicable)
        if (dest != 0)
        {
            // Only write if the written log level is less than or equal to the set level
            if (level <= fileLevel){

                DateTime date = DateTime.UtcNow;

                // Time fractions of a second
                String timestamp = date.TimeOfDay.ToString().Substring(0, 8);

                // Write Log
                writeToLogFile(timestamp + " [" + logLevels[level] + "] \t" + message, closeStream);
            }
        }
    }

    public void writeToLogFile(string message, Boolean closeStream=false)
    {
        message += '\n';
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);

        logStream.Seek(offset, SeekOrigin.Begin);
        logStream.Write(messageBytes, 0, messageBytes.Length);

        // update offset
        offset += messageBytes.Length;

        // move offset to half of the max size, captures both earliest half and latest half
        if(offset > maxSize)
        {
            offset = maxSize / 2;
        }

        if (closeStream)
        {
            logStream.Close();
        }
    }

    private void OnApplicationQuit()
    {
        // Close Log
        log("Application Quit!", INFO, -1, true);
    }
}
