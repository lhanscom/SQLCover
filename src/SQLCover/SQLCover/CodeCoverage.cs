﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using SQLCover.Gateway;
using SQLCover.Source;
using SQLCover.Trace;

namespace SQLCover
{
    public class CodeCoverage
    {
        private readonly DatabaseGateway _database;
        private readonly string _databaseName;
        private readonly bool _debugger;
        private readonly List<string> _excludeFilter;
        private readonly bool _logging;
        private readonly SourceGateway _source;
        private CoverageResult _result;

        private TraceController _trace;

        //This is to better support powershell and optional parameters
        public CodeCoverage(string connectionString, string databaseName) : this(connectionString, databaseName, null, false, false)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter) : this(connectionString, databaseName, excludeFilter, false, false)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter, bool logging) : this(connectionString, databaseName, excludeFilter, logging, false)
        {
        }

        public CodeCoverage(string connectionString, string databaseName, string[] excludeFilter, bool logging, bool debugger)
        {
            if (debugger)
                Debugger.Launch();
            
            _databaseName = databaseName;
            if (excludeFilter == null)
                excludeFilter = new string[0];

            _excludeFilter = excludeFilter.ToList();
            _logging = logging;
            _debugger = debugger;
            _database = new DatabaseGateway(connectionString, databaseName);
            _source = new DatabaseSourceGateway(_database);
        }

        public void Start()
        {
            _trace = new TraceControllerBuilder().GetTraceController(_database, _databaseName);
            _trace.Start();
        }

        private List<string> StopInternal()
        {
            var events = _trace.ReadTrace();
            _trace.Stop();
            _trace.Drop();

            return events;
        }

        public CoverageResult Stop()
        {
            WaitForTraceMaxLatency();

            var results = StopInternal();

            GenerateResults(_excludeFilter, results);

            return _result;
        }

        private void Debug(string message, params object[] args)
        {
            if (_logging)
                Console.WriteLine(message, args);
        }

        public CoverageResult Cover(string command)
        {
            try
            {
                Debug("Starting Code Coverage");

                Start();
                Debug("Starting Code Coverage...Done");

                Debug("Executing Command: {0}", command);
                try
                {
                    _database.Execute(command); //todo read messages or rowcounts or something
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception running command: {0} - error: {1}", command, e.Message);
                }

                Debug("Executing Command: {0}...done", command);
                WaitForTraceMaxLatency();
                Debug("Stopping Code Coverage");
                var rawEvents = StopInternal();
                Debug("Stopping Code Coverage...done");

                Debug("Getting Code Coverage Result");
                GenerateResults(_excludeFilter, rawEvents);
                Debug("Getting Code Coverage Result..done");
            }
            catch (Exception e)
            {
                Debug("Exception running code coverage: {0}\r\n{1}", e.Message, e.StackTrace);
            }

            return _result;
        }

        public CoverageResult CoverExe(string exe, string args, string workingDir = null)
        {
            try
            {
                Debug("Starting Code Coverage");

                Start();
                Debug("Starting Code Coverage...done");

                Debug("Executing Command: {0} {1} {2}", workingDir, exe, args);
                RunProcess(exe, args, workingDir);
                Debug("Executing Command: {0} {1} {2}...done", workingDir, exe, args);
                WaitForTraceMaxLatency();
                Debug("Stopping Code Coverage");
                var rawEvents = StopInternal();
                Debug("Stopping Code Coverage...done");

                Debug("Getting Code Coverage Result");
                GenerateResults(_excludeFilter, rawEvents);
                Debug("Getting Code Coverage Result..done");
                
            }
            catch (Exception e)
            {
                Debug("Exception running code coverage: {0}\r\n{1}", e.Message, e.StackTrace);
            }

            return _result;
        }

        private static void WaitForTraceMaxLatency()
        {
            Thread.Sleep(1000); //max distpatch latency!
        }

        private void RunProcess(string exe, string args, string workingDir)
        {
            var si = new ProcessStartInfo();
            si.FileName = exe;
            si.Arguments = args;
            si.UseShellExecute = false;
            si.WorkingDirectory = workingDir;

            var process = Process.Start(si);
            process.WaitForExit();
        }


        private void GenerateResults(List<string> filter, List<string> xml)
        {
            var batches = _source.GetBatches(filter);
            _result = new CoverageResult(batches, xml, _databaseName);
        }

        public CoverageResult Results()
        {
            return _result;
        }
    }
}