// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Siegfried Pammer" email="sie_pam@gmx.at"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.Profiler.Controller.Data.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using ICSharpCode.Profiler.Interprocess;

namespace ICSharpCode.Profiler.Controller.Data
{
	/// <summary>
	/// A profiling data provider based on a SQLite database stored in a file.
	/// </summary>
	public sealed class ProfilingDataSQLiteProvider : ProfilingDataProvider, IDisposable
	{
		SQLiteConnection connection;
		bool isDisposed;
		Dictionary<int, NameMapping> nameMappingCache;
		ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
		ReadOnlyCollection<IProfilingDataSet> dataSets;
		
		/// <summary>
		/// Creates a new SQLite profiling data provider and opens a database stored in a file.
		/// </summary>
		public ProfilingDataSQLiteProvider(string fileName)
		{
			this.nameMappingCache = new Dictionary<int, NameMapping>();
			
			SQLiteConnectionStringBuilder conn = new SQLiteConnectionStringBuilder();
			conn.Add("Data Source", fileName);
			this.connection = new SQLiteConnection(conn.ConnectionString);
			
			this.connection.Open();
		}
		
		/// <summary>
		/// Creates a new SQLite profiling data provider from a file.
		/// </summary>
		public static ProfilingDataSQLiteProvider FromFile(string fileName)
		{
			return new ProfilingDataSQLiteProvider(fileName);
		}
		
		/// <inheritdoc/>
		public override void Close()
		{
			this.Dispose();
		}
		
		internal IQueryable<CallTreeNode> GetChildren(SQLiteCallTreeNode parent)
		{
			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd)) {
				cmd.CommandText = @"SELECT id, nameid, callcount, timespent, isactiveatstart
									FROM FunctionData
									WHERE parentid IN(" + string.Join(",", parent.ids.Select(a => a.ToString(CultureInfo.InvariantCulture.NumberFormat)).ToArray()) + @")
									ORDER BY id;";

				using (SQLiteDataReader reader = cmd.ExecuteReader()) {
					List<SQLiteCallTreeNode> items = new List<SQLiteCallTreeNode>();

					while (reader.Read()) {
						int childNameId = reader.GetInt32(1);
						SQLiteCallTreeNode newItem = items.Find(node => node.nameId == childNameId);
						if (newItem == null) {
							newItem = new SQLiteCallTreeNode(childNameId, (parent.nameId == 0) ? null : parent, this);
							items.Add(newItem);

							// works because of ORDER BY id
							newItem.isActiveAtStart = reader.GetBoolean(4);
						}

						newItem.callCount += reader.GetInt32(2);
						newItem.cpuCyclesSpent += (ulong)reader.GetInt64(3);
						newItem.ids.Add(reader.GetInt32(0));
					}

					return items.Cast<CallTreeNode>().AsQueryable();
				}
			}
		}
		
		internal IQueryable<CallTreeNode> GetCallers(SQLiteCallTreeNode item)
		{
			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd)) {
				cmd.CommandText = @"SELECT id, nameid, callcount, timespent, isactiveatstart
									FROM FunctionData
									WHERE id IN(
										SELECT parentid
										FROM FunctionData
										WHERE id IN(" + string.Join(",", item.ids.Select(s => s.ToString()).ToArray()) + @")
									)
									ORDER BY id;";
				
				Debug.Print("GetCallers cmd: " + cmd.CommandText);

				using (SQLiteDataReader reader = cmd.ExecuteReader()) {
					List<SQLiteCallTreeNode> items = new List<SQLiteCallTreeNode>();

					while (reader.Read()) {
						int childNameId = reader.GetInt32(1);
						SQLiteCallTreeNode newItem = items.Find(node => node.nameId == childNameId);
						if (newItem == null) {
							newItem = new SQLiteCallTreeNode(childNameId, null, this);
							items.Add(newItem);

							// works because of ORDER BY id
							newItem.isActiveAtStart = reader.GetBoolean(4);
						}

						newItem.callCount += reader.GetInt32(2);
						newItem.cpuCyclesSpent += (ulong)reader.GetInt64(3);
						newItem.ids.Add(reader.GetInt32(0));
					}

					return items.Cast<CallTreeNode>().AsQueryable(); // TODO : remove Cast<> in .NET 4.0
				}
			}
		}
		
		/// <inheritdoc/>
		public override NameMapping GetMapping(int nameId)
		{
			lock (nameMappingCache) {
				if (nameMappingCache.ContainsKey(nameId))
					return nameMappingCache[nameId];
				
				SQLiteCommand cmd;
				using (LockAndCreateCommand(out cmd)) {
					cmd.CommandText = @"SELECT name, returntype, parameters
										FROM NameMapping
										WHERE id = " + nameId + ";";
					
					using (SQLiteDataReader reader = cmd.ExecuteReader()) {
						
						string name = null, returnType = null;
						IList<string> parameters = null;
						
						while (reader.Read()) {
							name = reader.GetString(0);
							returnType = reader.GetString(1);
							parameters = reader.GetString(2).Split('-').ToList();
							if (parameters.Count == 1 && string.IsNullOrEmpty(parameters[0]))
								parameters = new List<string>();
						}
						
						NameMapping map = new NameMapping(nameId, returnType, name, parameters);
						nameMappingCache.Add(nameId, map);
						return map;
					}
				}
			}
		}
		
		/// <inheritdoc/>
		public override ReadOnlyCollection<IProfilingDataSet> DataSets {
			get {
				if (this.dataSets == null) {
					List<IProfilingDataSet> list = new List<IProfilingDataSet>();
					
					SQLiteCommand cmd;
					
					using (LockAndCreateCommand(out cmd)) {
						SQLiteDataReader reader;
						bool isFirstAllowed = true;
						try {
							cmd.CommandText = @"SELECT id, cpuusage, isfirst
											FROM DataSets
											ORDER BY id;";
							
							reader = cmd.ExecuteReader();
						} catch (SQLiteException) {
							cmd.CommandText = @"SELECT id, cpuusage
											FROM DataSets
											ORDER BY id;";
							
							reader = cmd.ExecuteReader();
							
							isFirstAllowed = false;
						}
						
						while (reader.Read()) {
							list.Add(new SQLiteDataSet(this, reader.GetInt32(0), reader.GetDouble(1), isFirstAllowed ? reader.GetBoolean(2) : false));
						}
					}
					
					this.dataSets = new ReadOnlyCollection<IProfilingDataSet>(list);
				}
				
				return this.dataSets;
			}
		}
		
		double GetCpuUsage(int index) {
			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd)) {
				cmd.CommandText = @"SELECT cpuusage
									FROM DataSets
									WHERE id = " + index + ";";
				
				using (SQLiteDataReader reader = cmd.ExecuteReader()) {
					while (reader.Read())
						return reader.GetDouble(0);

					return 0;
				}
			}
		}
		
		class SQLiteDataSet : IProfilingDataSet
		{
			ProfilingDataSQLiteProvider provider;
			int index;
			double cpuUsage;
			bool isFirst;
			
			public SQLiteDataSet(ProfilingDataSQLiteProvider provider, int index, double cpuUsage, bool isFirst)
			{
				this.provider = provider;
				this.index = index;
				this.cpuUsage = cpuUsage;
				this.isFirst = isFirst;
			}
			
			public double CpuUsage {
				get {
					return cpuUsage;
				}
			}
			
			public CallTreeNode RootNode {
				get {
					return this.provider.GetRoot(index, index);
				}
			}
			
			public bool IsFirst {
				get {
					return isFirst;
				}
			}
		}
		
		/// <summary>
		/// Closes the connection to the database.
		/// </summary>
		public void Dispose()
		{
			rwLock.EnterWriteLock();
			try {
				if (!isDisposed)
					this.connection.Close();
				
				isDisposed = true;
			} finally {
				rwLock.ExitWriteLock();
			}
		}
		
		/// <inheritdoc/>
		public override CallTreeNode GetRoot(int startIndex, int endIndex)
		{
			if (startIndex > endIndex) {
				int help = startIndex;
				startIndex = endIndex;
				endIndex = help;
			}

			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd)) {
				cmd.CommandText = @"SELECT id, nameid, callcount, timespent
									FROM FunctionData
									WHERE id IN(
										SELECT rootid
										FROM DataSets
										WHERE id BETWEEN " + startIndex + " AND " + endIndex + @"
									)
									ORDER BY id;";
				
				using (SQLiteDataReader reader = cmd.ExecuteReader()) {
					SQLiteCallTreeNode root = new SQLiteCallTreeNode(0, null, this);

					while (reader.Read()) {
						root.callCount += reader.GetInt32(2);
						root.cpuCyclesSpent += (ulong)reader.GetInt64(3);
						root.ids.Add(reader.GetInt32(0));
					}

					return root;
				}
			}
		}
		
		/// <inheritdoc/>
		public override void SetProperty(string name, string value)
		{
			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd))
				SetProperty(cmd, name, value);
		}
		
		internal static void SetProperty(SQLiteCommand cmd, string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException("name");
			
			cmd.Parameters.Add(new SQLiteParameter("@name", name));
			if (value == null) {
				cmd.CommandText = "DELETE FROM Properties WHERE name=@name;";
			} else {
				cmd.Parameters.Add(new SQLiteParameter("@value", value));
				cmd.CommandText = "INSERT OR REPLACE INTO Properties(name, value) VALUES(@name, @value);";
			}
			cmd.ExecuteNonQuery();
		}
		
		/// <inheritdoc/>
		public override string GetProperty(string name)
		{
			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd))
				return GetProperty(cmd, name);
		}
		
		internal static string GetProperty(SQLiteCommand cmd, string name)
		{
			if (name == null)
				throw new ArgumentNullException("name");
			
			cmd.Parameters.Add(new SQLiteParameter("@name", name));
			cmd.CommandText = "SELECT value FROM Properties WHERE name = ?;";
			SQLiteDataReader reader = cmd.ExecuteReader();
			
			if (reader.Read()) {
				return reader.GetString(0);
			}
			
			return null;
		}
		
		int processorFrequency = -1;
		
		/// <inheritdoc/>
		public override int ProcessorFrequency {
			get {
				if (processorFrequency == -1) {
					string value = GetProperty("processorfrequency");
					if (value != null)
						processorFrequency = int.Parse(value, CultureInfo.InvariantCulture);
					else
						throw new ProfilerException("processorfrequency was not found!");
				}
				
				return this.processorFrequency;
			}
		}
		
		internal IQueryable<CallTreeNode> CreateQuery(QueryNode query)
		{
			return new IQToolkit.Query<CallTreeNode>(new SQLiteQueryProvider(this), query);
		}
		
		/// <inheritdoc/>
		public override IQueryable<CallTreeNode> GetFunctions(int startIndex, int endIndex)
		{
			Expression<Func<SingleCall, bool>> filterLambda = c => startIndex <= c.DataSetID && c.DataSetID <= endIndex;
			return CreateQuery(new MergeByName(new Filter(AllCalls.Instance, filterLambda)));
		}
		
		/*
			if (startIndex < 0 || startIndex >= this.dataSets.Count)
				throw new ArgumentOutOfRangeException("startIndex", startIndex, "Value must be between 0 and " + endIndex);
			if (endIndex < startIndex || endIndex >= this.DataSets.Count)
				throw new ArgumentOutOfRangeException("endIndex", endIndex, "Value must be between " + startIndex + " and " + (this.DataSets.Count - 1));
			
			IList<CallTreeNode> functions = new List<CallTreeNode>();
			
			SQLiteCommand cmd;
			using (LockAndCreateCommand(out cmd)) {
				cmd.CommandText = @"SELECT GROUP_CONCAT(id), nameid, SUM(timespent), SUM(callcount)
									FROM FunctionData
									WHERE datasetid BETWEEN @start AND @end
									GROUP BY nameid;";
				cmd.Parameters.Add(new SQLiteParameter("@start", startIndex));
				cmd.Parameters.Add(new SQLiteParameter("@end", endIndex));
				
				using (SQLiteDataReader reader = cmd.ExecuteReader()) {
					
					while (reader.Read()) {
						SQLiteCallTreeNode node = new SQLiteCallTreeNode(reader.GetInt32(1), null, this);
						node.callCount = reader.GetInt32(3);
						node.cpuCyclesSpent = (ulong)reader.GetInt64(2);
						node.ids = reader.GetString(0).Split(',').Select(s => int.Parse(s)).ToList();
						node.ids.Sort();
						// Can not do filtering of root and thread nodes here,
						// because retrieval of names needs access to DB
						// which is forbidden now (we are inside the lock!)
						functions.Add(node);
					}
				}
			}
			
			// Do filtering now via LINQ
			return functions.SkipWhile(i => i.NameMapping.Id == 0 || i.NameMapping.Name.StartsWith("Thread#", StringComparison.Ordinal)).AsQueryable();
		}
		 */
		
		LockObject LockAndCreateCommand(out SQLiteCommand cmd)
		{
			this.rwLock.EnterReadLock();
			
			if (isDisposed) {
				this.rwLock.ExitReadLock();
				throw new ObjectDisposedException("ProfilingDataSQLiteProvider", "The provider was already closed!");
			}
			
			cmd = this.connection.CreateCommand();
			return new LockObject(cmd, this.rwLock);
		}
		
		struct LockObject : IDisposable
		{
			SQLiteCommand cmd;
			ReaderWriterLockSlim rwLock;
			
			public LockObject(SQLiteCommand cmd, ReaderWriterLockSlim rwLock)
			{
				this.rwLock = rwLock;
				this.cmd = cmd;
			}
			
			public void Dispose()
			{
				cmd.Dispose();
				rwLock.ExitReadLock();
			}
		}
	}
}
