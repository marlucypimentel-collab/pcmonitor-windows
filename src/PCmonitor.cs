using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Threading;
internal static class PCmonitor {
 static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PCmonitor","data");
 static readonly string Key="Local\\PCmonitor_"+Environment.UserName;
 static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
 static readonly Encoding Utf8=new UTF8Encoding(true);
 static readonly string Session=Guid.NewGuid().ToString("N");
 static double TotalMB; static Stopwatch Clock=Stopwatch.StartNew(); static double PrevTime;
 sealed class Proc {public int Id; public string Name; public long Start; public double Cpu,Ram;}
 static Dictionary<int,Proc> Previous=new Dictionary<int,Proc>();
 static string F(double x){return x.ToString("0.00",Inv);}
 static string Cell(object x){return "\""+Convert.ToString(x,Inv).Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";}
 static void Row(string type,string header,params object[] values){
  string path=Path.Combine(Data,type+"-"+DateTime.Now.ToString("yyyy-MM-dd")+".csv");
  bool fresh=!File.Exists(path)||new FileInfo(path).Length==0;
  using(var w=new StreamWriter(path,true,Utf8)){if(fresh)w.WriteLine(header);w.WriteLine(string.Join(";",values.Select(Cell).ToArray()));}
 }
 static void Error(string text){try{File.AppendAllText(Path.Combine(Data,"errors-"+DateTime.Now.ToString("yyyy-MM-dd")+".txt"),DateTimeOffset.Now.ToString("o")+" "+text+Environment.NewLine,Utf8);}catch{}}
 static string Phase(){try{return File.ReadAllText(Path.Combine(Data,"phase.txt")).Trim()=="depois"?"depois":"antes";}catch{return "antes";}}
 static Dictionary<string,object> Query(string sql){
  var result=new Dictionary<string,object>();
  try{using(var s=new ManagementObjectSearcher("root\\cimv2",sql)){
   s.Options.Timeout=TimeSpan.FromSeconds(10);
   using(var list=s.Get()){foreach(ManagementObject item in list){using(item){foreach(PropertyData p in item.Properties)result[p.Name]=p.Value;}break;}}
  }if(result.Count==0)Error("WMI returned no data: "+sql);}catch(Exception e){Error(e.Message);}
  return result;
 }
 static object Num(Dictionary<string,object> d,string key){object v;return d.TryGetValue(key,out v)&&v!=null?(object)Convert.ToDouble(v,Inv):"";}
 static void Processes(string stamp,string phase){
  double now=Clock.Elapsed.TotalSeconds,elapsed=now-PrevTime;
  var current=new Dictionary<int,Proc>();var ranked=new List<Tuple<Proc,double>>();
  foreach(Process p in Process.GetProcesses())using(p){try{
   if(p.Id==0)continue;
   var item=new Proc{Id=p.Id,Name=p.ProcessName,Start=p.StartTime.ToUniversalTime().Ticks,Cpu=p.TotalProcessorTime.TotalSeconds,Ram=p.WorkingSet64/1048576.0};
   current[item.Id]=item;Proc old;double cpu=-1;
   if(Previous.TryGetValue(item.Id,out old)&&old.Start==item.Start&&elapsed>0)cpu=Math.Max(0,Math.Min(100,(item.Cpu-old.Cpu)/elapsed/Environment.ProcessorCount*100));
   ranked.Add(Tuple.Create(item,cpu));
  }catch{/* Protected or exited process; monitor runs without elevation. */}}
  var selected=ranked.OrderByDescending(x=>x.Item2).Take(10).Concat(ranked.OrderByDescending(x=>x.Item1.Ram).Take(10)).GroupBy(x=>x.Item1.Id).Select(g=>g.First());
  foreach(var item in selected)Row("processes","timestamp;phase;session;pid;name;cpu_pct_total_machine;working_set_mb;window_seconds",stamp,phase,Session,item.Item1.Id,item.Item1.Name,item.Item2<0?"":F(item.Item2),F(item.Item1.Ram),F(elapsed));
  Previous=current;PrevTime=now;
 }
 static void Sample(){
  string stamp=DateTimeOffset.Now.ToString("o"),phase=Phase();
  var cpu=Query("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
  var mem=Query("SELECT AvailableMBytes,CommittedBytes,CommitLimit,PagesPersec FROM Win32_PerfFormattedData_PerfOS_Memory");
  object available=Num(mem,"AvailableMBytes");
  object used=available is double&&TotalMB>0?(object)F((TotalMB-(double)available)/TotalMB*100):"";
  Row("resources","timestamp;phase;session;cpu_pct;ram_total_mb;ram_available_mb;ram_used_pct;committed_bytes;commit_limit_bytes;pages_per_sec",stamp,phase,Session,Num(cpu,"PercentProcessorTime"),F(TotalMB),available,used,Num(mem,"CommittedBytes"),Num(mem,"CommitLimit"),Num(mem,"PagesPersec"));
  try{using(var s=new ManagementObjectSearcher("SELECT Name,PercentIdleTime,CurrentDiskQueueLength,DiskReadBytesPersec,DiskWriteBytesPersec,DiskTransfersPersec FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk")){
   s.Options.Timeout=TimeSpan.FromSeconds(10);
   using(var list=s.Get())foreach(ManagementObject d in list)using(d){
    object busy=d["PercentIdleTime"]==null?(object)"":F(Math.Max(0,Math.Min(100,100-Convert.ToDouble(d["PercentIdleTime"],Inv))));
    Row("disks","timestamp;phase;session;disk;busy_pct;queue_length;read_bytes_sec;write_bytes_sec;transfers_sec",stamp,phase,Session,d["Name"],busy,d["CurrentDiskQueueLength"],d["DiskReadBytesPersec"],d["DiskWriteBytesPersec"],d["DiskTransfersPersec"]);
   }
  }}catch(Exception e){Error(e.Message);}
  foreach(DriveInfo drive in DriveInfo.GetDrives())try{if(drive.DriveType==DriveType.Fixed&&drive.IsReady)Row("volumes","timestamp;phase;session;volume;total_bytes;free_bytes",stamp,phase,Session,drive.Name,drive.TotalSize,drive.TotalFreeSpace);}catch(Exception e){Error(e.Message);}
  Processes(stamp,phase);
  File.WriteAllText(Path.Combine(Data,"status.txt"),"PCmonitor ativo\r\nUltima coleta: "+stamp+"\r\nFase: "+phase+"\r\nPID: "+Process.GetCurrentProcess().Id+"\r\nDados: "+Data+"\r\n",Utf8);
 }
 static void Metadata(){
  var os=Query("SELECT Caption,Version,BuildNumber,TotalVisibleMemorySize,LastBootUpTime FROM Win32_OperatingSystem");
  object ram=Num(os,"TotalVisibleMemorySize");TotalMB=ram is double?(double)ram/1024:0;
  using(var w=new StreamWriter(Path.Combine(Data,"session-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Session.Substring(0,8)+".txt"),false,Utf8)){
   w.WriteLine("PCmonitor 1.0; interval=30 seconds; logical_processors="+Environment.ProcessorCount);
   w.WriteLine("Started="+DateTimeOffset.Now.ToString("o")+"; phase="+Phase());
   foreach(var pair in os)w.WriteLine(pair.Key+"="+pair.Value);
   foreach(string sql in new[]{"SELECT Name FROM Win32_Processor","SELECT Model,MediaType,Size FROM Win32_DiskDrive"})foreach(var pair in Query(sql))w.WriteLine(pair.Key+"="+pair.Value);
  }
 }
 [STAThread]static void Main(string[] args){try{
  Directory.CreateDirectory(Data);string command=args.Length>0?args[0]:"--run";
  if(command=="--before"||command=="--after"){
   string phase=command=="--after"?"depois":"antes";
   File.WriteAllText(Path.Combine(Data,"phase.txt"),phase,Utf8);
   File.AppendAllText(Path.Combine(Data,"markers.txt"),DateTimeOffset.Now.ToString("o")+";"+phase+Environment.NewLine,Utf8);return;
  }
  if(command=="--stop"){try{using(var e=EventWaitHandle.OpenExisting(Key+"_Stop"))e.Set();}catch(WaitHandleCannotBeOpenedException){}return;}
  bool created;using(var mutex=new Mutex(true,Key,out created)){
   if(!created)return;
   try{using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,Key+"_Stop")){
    stop.Reset();Metadata();
    File.AppendAllText(Path.Combine(Data,"events.txt"),DateTimeOffset.Now.ToString("o")+";started;"+Session+Environment.NewLine,Utf8);
    while(!stop.WaitOne(0)){
     var timer=Stopwatch.StartNew();try{Sample();}catch(Exception e){Error(e.ToString());}
     if(stop.WaitOne((int)Math.Max(1000,30000-timer.ElapsedMilliseconds)))break;
    }
    File.WriteAllText(Path.Combine(Data,"status.txt"),"PCmonitor parado em "+DateTimeOffset.Now.ToString("o"),Utf8);
    File.AppendAllText(Path.Combine(Data,"events.txt"),DateTimeOffset.Now.ToString("o")+";stopped;"+Session+Environment.NewLine,Utf8);
   }}finally{mutex.ReleaseMutex();}
  }
 }catch(Exception e){Error(e.ToString());}}
}