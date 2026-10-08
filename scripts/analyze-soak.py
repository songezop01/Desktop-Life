"""Describe native soak measurements; never equate a short trend with proof of no leak."""
import argparse, json, math, statistics
from pathlib import Path

EXPECTED_GEOMETRY_REASON = "geometry-changed-in-flight"
MAX_RECOVERY_SECONDS = 5


def finite_number(value):
    return type(value) in (int, float) and math.isfinite(value)


def percentile(values, quantile):
    if not values:
        return None
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * quantile) - 1)]


def is_counter(value):
    return type(value) is int and value >= 0


def valid_reasons(value):
    return isinstance(value, dict) and all(isinstance(reason, str) and reason and type(count) is int and count > 0 for reason, count in value.items())


def bounded_duration(value):
    return type(value) in (int, float) and math.isfinite(value) and 0 <= value <= MAX_RECOVERY_SECONDS


def approach_assessment(report):
    """Do not let a fallback hide a stalled furniture/house approach."""
    counters = [report.get("ApproachTimeouts")]
    by_character = report.get("CharacterApproachTimeouts")
    if isinstance(by_character, dict):
        counters.extend(by_character.values())
    counters.extend(sample.get("ApproachTimeouts") for sample in report["Samples"])
    details = report.get("CharacterApproachTimeoutDetails", {})
    if any(is_counter(count) and count > 0 for count in counters) or (
            isinstance(details, dict) and any(isinstance(rows, list) and rows for rows in details.values())):
        return "FAIL", "Approach timeout", "A furniture or house approach exceeded its progress budget before fallback."
    # Historical 0.10.x baselines did not retain this telemetry. Their comparison
    # remains readable, but they cannot supply the required 0.11 installation gate.
    candidate = any(key in report for key in ("Floors", "HouseRouteFailures", "ApproachTimeouts")) or str(report.get("Version", "")).startswith("0.11")
    if not candidate:
        return "PASS", "Not assessed: legacy report lacks approach-timeout telemetry", None
    characters = report.get("Characters")
    if (not is_counter(report.get("ApproachTimeouts"))
            or not isinstance(characters, list) or not characters
            or not isinstance(by_character, dict) or set(by_character) != set(characters)
            or any(not is_counter(count) for count in by_character.values())
            or report["ApproachTimeouts"] != sum(by_character.values())
            or any(not is_counter(sample.get("ApproachTimeouts")) for sample in report["Samples"])
            or not isinstance(details, dict) or set(details) != set(characters)
            or any(not isinstance(rows, list) for rows in details.values())):
        return "REVIEW", "Incomplete approach-timeout telemetry", "0.11 requires consistent aggregate, per-character, sample and retained approach-timeout evidence."
    return "PASS", "No approach timeouts", None


def navigation_assessment(report):
    """Preserve raw failures; exempt only fully reconciled, bounded geometry recoveries."""
    timeouts = report.get("NavigationRecoveryTimeouts", 0)
    character_timeouts = report.get("CharacterNavigationRecoveryTimeouts", {})
    timeout_reasons = report.get("NavigationRecoveryTimeoutReasons", {})
    character_timeout_reasons = report.get("CharacterNavigationRecoveryTimeoutReasons", {})
    if ((is_counter(timeouts) and timeouts > 0)
            or (isinstance(character_timeouts, dict) and any(is_counter(count) and count > 0 for count in character_timeouts.values()))
            or (valid_reasons(timeout_reasons) and sum(timeout_reasons.values()) > 0)
            or (isinstance(character_timeout_reasons, dict) and any(valid_reasons(reasons) and sum(reasons.values()) > 0 for reasons in character_timeout_reasons.values()))):
        return "FAIL", "Recovery timeout", "Navigation recovery timeout; a later escape cannot erase this failure."
    details = report.get("CharacterNavigationRecoveryDetails", {})
    if isinstance(details, dict) and any(isinstance(detail, dict) and detail.get("TimedOut") is True for rows in details.values() if isinstance(rows, list) for detail in rows):
        return "FAIL", "Recovery timeout", "Retained recovery details record a timeout."
    failures = report["NavigationFailures"]
    if not is_counter(failures):
        return "REVIEW", "Invalid navigation telemetry", "Navigation failure count is not a nonnegative integer."
    if failures == 0:
        for key in ("NavigationRecoveryAttempts", "ExpectedGeometryChangeRecoveryAttempts", "UnexpectedNavigationRecoveryAttempts", "NavigationRecoveriesCompleted", "NavigationRecoveryTimeouts"):
            if key in report and (not is_counter(report[key]) or report[key] != 0):
                return "REVIEW", "Inconsistent navigation telemetry", "Recovery counters disagree with zero navigation failures."
        for key in ("NavigationFailureReasons", "PrimaryNavigationReasons", "SecondaryNavigationReasons", "NavigationRecoveryCompletionReasons", "NavigationRecoveryTimeoutReasons"):
            if key in report and report[key] != {}:
                return "REVIEW", "Inconsistent navigation telemetry", "Navigation reason counters disagree with zero navigation failures."
        for key in ("CharacterNavigationReasons", "CharacterNavigationRecoveryCompletionReasons", "CharacterNavigationRecoveryTimeoutReasons"):
            if key in report and (not isinstance(report[key], dict) or any(value != {} for value in report[key].values())):
                return "REVIEW", "Inconsistent navigation telemetry", "Character reason counters disagree with zero navigation failures."
        for key in ("CharacterNavigationRecoveriesCompleted", "CharacterNavigationRecoveryTimeouts"):
            if key in report and (not isinstance(report[key], dict) or any(not is_counter(value) or value != 0 for value in report[key].values())):
                return "REVIEW", "Inconsistent navigation telemetry", "Character recovery counters disagree with zero navigation failures."
        if "NavigationRecoveryCompletionMaxSeconds" in report and (not bounded_duration(report["NavigationRecoveryCompletionMaxSeconds"]) or report["NavigationRecoveryCompletionMaxSeconds"] != 0):
            return "REVIEW", "Inconsistent navigation telemetry", "Recovery duration disagrees with zero navigation failures."
        if "CharacterNavigationRecoveryCompletionMaxSeconds" in report and (not isinstance(report["CharacterNavigationRecoveryCompletionMaxSeconds"], dict) or any(not bounded_duration(value) or value != 0 for value in report["CharacterNavigationRecoveryCompletionMaxSeconds"].values())):
            return "REVIEW", "Inconsistent navigation telemetry", "Character recovery duration disagrees with zero navigation failures."
        if not isinstance(details, dict) or any(not isinstance(rows, list) or rows for rows in details.values()):
            return "REVIEW", "Inconsistent navigation telemetry", "Recovery details disagree with zero navigation failures."
        return "PASS", "No navigation failures", None
    expected = {EXPECTED_GEOMETRY_REASON: failures}
    required_counts = {"NavigationRecoveryAttempts": failures, "ExpectedGeometryChangeRecoveryAttempts": failures,
                       "UnexpectedNavigationRecoveryAttempts": 0, "NavigationRecoveriesCompleted": failures, "NavigationRecoveryTimeouts": 0}
    if any(not is_counter(report.get(key)) or report[key] != value for key, value in required_counts.items()):
        return "REVIEW", "Unconfirmed navigation recovery", "Navigation failures include unexpected, unfinished or unreconciled recoveries."
    if any(not valid_reasons(report.get(key)) for key in ("NavigationFailureReasons", "NavigationRecoveryCompletionReasons", "NavigationRecoveryTimeoutReasons")) or report["NavigationFailureReasons"] != expected or report["NavigationRecoveryCompletionReasons"] != expected or report["NavigationRecoveryTimeoutReasons"] != {}:
        return "REVIEW", "Unconfirmed navigation recovery", "Failure and completion reasons do not reconcile as geometry-only recoveries."
    split_reasons = [report.get(key) for key in ("PrimaryNavigationReasons", "SecondaryNavigationReasons")]
    if any(not valid_reasons(reasons) for reasons in split_reasons):
        return "REVIEW", "Incomplete navigation telemetry", "Primary or secondary failure reason counters are missing or invalid."
    merged_reasons = {}
    for reasons in split_reasons:
        for reason, count in reasons.items():
            merged_reasons[reason] = merged_reasons.get(reason, 0) + count
    if merged_reasons != expected:
        return "REVIEW", "Unreconciled navigation telemetry", "Primary and secondary reasons contradict aggregate geometry-only failure counters."
    characters = report.get("Characters")
    if not isinstance(characters, list) or not characters or any(not isinstance(kind, str) or not kind for kind in characters) or len(set(characters)) != len(characters):
        return "REVIEW", "Incomplete character telemetry", "Included character identities are missing or invalid."
    keys = set(characters)
    mappings = [report.get(key) for key in ("CharacterNavigationReasons", "CharacterNavigationRecoveriesCompleted", "CharacterNavigationRecoveryTimeouts", "CharacterNavigationRecoveryCompletionReasons", "CharacterNavigationRecoveryTimeoutReasons", "CharacterNavigationRecoveryCompletionMaxSeconds")]
    if any(not isinstance(mapping, dict) or set(mapping) != keys for mapping in mappings):
        return "REVIEW", "Incomplete character telemetry", "Recovery evidence does not cover every included character."
    reasons, completed, timed_out, completion_reasons, timeout_reasons, max_seconds = mappings
    total = 0
    for character in characters:
        failure_reasons = reasons[character]
        if not valid_reasons(failure_reasons) or any(reason != EXPECTED_GEOMETRY_REASON for reason in failure_reasons):
            return "REVIEW", "Unexpected navigation reason", "An included character has an unexpected or invalid navigation reason."
        count = sum(failure_reasons.values())
        total += count
        if (not is_counter(completed[character]) or completed[character] != count or not is_counter(timed_out[character]) or timed_out[character] != 0
                or not valid_reasons(completion_reasons[character]) or completion_reasons[character] != failure_reasons
                or not valid_reasons(timeout_reasons[character]) or timeout_reasons[character] != {}):
            return "REVIEW", "Unreconciled character recovery", "A character's failures and actual recovery completions do not reconcile."
        if not bounded_duration(max_seconds[character]) or (count == 0 and max_seconds[character] != 0):
            return "REVIEW", "Unbounded recovery", "Active simulation recovery duration does not prove completion within five seconds."
    global_max = report.get("NavigationRecoveryCompletionMaxSeconds")
    if total != failures or not bounded_duration(global_max) or global_max != max(max_seconds.values()):
        return "REVIEW", "Unreconciled navigation telemetry", "Aggregate counts or maximum recovery duration disagree with character evidence."
    if not isinstance(details, dict) or any(kind not in keys or not isinstance(rows, list) for kind, rows in details.items()):
        return "REVIEW", "Invalid recovery detail", "Retained recovery details contain invalid character identities or lists."
    for rows in details.values():
        if any(not isinstance(detail, dict) or detail.get("Reason") != EXPECTED_GEOMETRY_REASON or detail.get("Grounded") is not True or detail.get("ReachedEscape") is not True or detail.get("TimedOut") is not False or not bounded_duration(detail.get("ElapsedSeconds")) for detail in rows):
            return "REVIEW", "Contradictory recovery detail", "Retained details contradict grounded, bounded geometry recovery counters."
    return "PASS", "Expected geometry recoveries confirmed", None


parser = argparse.ArgumentParser()
parser.add_argument("report", type=Path)
parser.add_argument("--output", type=Path)
args = parser.parse_args()
report = json.loads(args.report.read_text(encoding="utf-8-sig"))
samples = report["Samples"]
if not samples:
    raise SystemExit("No samples")
metrics = ["WorkingSetMB", "PrivateMB", "ManagedBytes", "HandleCount", "GdiObjects", "UserObjects", "NativeWindows", "Windows", "RoomWindows", "ToyWindows", "ArtWindows", "ArtworkCount", "KeptArtworkCount", "ArtworkPoints", "DecodedCompanionAtlases"]
summary = {key: report[key] for key in ["Seconds", "RequestedSeconds", "Exceptions", "StuckSequences", "NavigationFailures", "PathPlans", "MachineCpuPercent", "AllocatedMB", "PhaseFrames"]}
summary["CompletedRequestedDuration"] = report["Seconds"] >= report["RequestedSeconds"] and samples[-1]["Second"] >= report["RequestedSeconds"]
summary["Metrics"] = {}
# Exclude the first 20 minutes from trend fitting; retain warm-up data separately.
steady = [s for s in samples if finite_number(s.get("WallSeconds")) and s["WallSeconds"] >= 1200]
for metric in metrics:
    values = [s[metric] for s in samples if finite_number(s.get(metric))]
    if not values:
        continue
    buckets = {}
    for s in samples:
        if finite_number(s.get(metric)) and finite_number(s.get("WallSeconds")):
            buckets.setdefault(int(s["WallSeconds"] // 600), []).append(s[metric])
    trend = [s for s in steady if finite_number(s.get(metric))]
    span = trend[-1]["WallSeconds"]-trend[0]["WallSeconds"] if len(trend)>1 else 0
    adequate = len(trend)>=30 and span>=1800
    slope = None
    if len(trend) >= 3:
        times = [s["WallSeconds"] / 3600 for s in trend]
        mean_t, mean_y = statistics.mean(times), statistics.mean(s[metric] for s in trend)
        variance = sum((t-mean_t)**2 for t in times)
        slope = sum((t-mean_t)*(s[metric]-mean_y) for t,s in zip(times,trend)) / variance if variance else None
    summary["Metrics"][metric] = {"First": values[0], "Median": statistics.median(values), "P50": percentile(values,.5), "P95": percentile(values,.95), "Last": values[-1], "Min": min(values), "Max": max(values), "PostWarmupSlopePerHour": slope, "PostWarmupSamples": len(trend), "PostWarmupSpanSeconds": span, "TrendSampleAdequate": adequate, "InvalidSamples": sum(metric in s and not finite_number(s[metric]) for s in samples), "TenMinuteMedians": {str(k): statistics.median(v) for k,v in buckets.items()}}
last = samples[-1]
summary["GCCollectionsAtEnd"] = {key: last.get(key) for key in ["Gen0", "Gen1", "Gen2"]}
summary["AllocationMiBPerMinute"] = report["AllocatedMB"] / (report["Seconds"] / 60)
cpu_intervals = [100 * (b["CpuSeconds"]-a["CpuSeconds"])/(b["WallSeconds"]-a["WallSeconds"]) for a,b in zip(samples,samples[1:]) if b["WallSeconds"]>a["WallSeconds"]]
summary["PeakOneCoreCpuPercent"] = max(cpu_intervals, default=None)
summary["CpuIntervals"] = {"Samples": len(cpu_intervals), "P50OneCorePercent": percentile(cpu_intervals,.5), "P95OneCorePercent": percentile(cpu_intervals,.95), "Note": "Percentiles of retained sample intervals, typically 30 seconds; not per-frame CPU or FPS."}
summary["UiLatency"] = {}
for phase, key in (("Setup","SetupPerformance"),("Steady","Performance")):
    performance = report.get(key)
    if isinstance(performance,dict):
        summary["UiLatency"][phase] = {name: performance[name] for name in ("DispatcherSamples","DispatcherDelayMeanMs","DispatcherDelayP50UpperMs","DispatcherDelayP95UpperMs","DispatcherDelayMaxMs","DispatcherDelaysOver50Ms","SaveCount","SaveMeanMs","SaveP50UpperMs","SaveP95UpperMs","SaveMaxMs","SnapshotCaptureCount","SnapshotCaptureMeanMs","SnapshotCaptureP50UpperMs","SnapshotCaptureP95UpperMs","SnapshotCaptureMaxMs") if name in performance}
        summary["UiLatency"][phase]["PercentileNote"] = "Nearest-rank histogram upper bounds: dispatcher buckets 10 ms, synchronous barrier and UI snapshot buckets 1 ms; overflow uses observed maximum."
summary["Persistence"] = {phase: report[key] for phase,key in (("Workload","Performance"),("AfterDurableReload","DurableReloadPerformance")) if isinstance(report.get(key),dict) and any(name in report[key] for name in ("Persistence","PersistenceObservation"))}
summary["Persistence"] = {phase: {key: value for key,value in stats.items() if key in ("Persistence","PersistenceObservation")} for phase,stats in summary["Persistence"].items()}
summary["RecoveryCount"] = report.get("RecoveryCount")
summary["UnreachableTargets"] = report.get("UnreachableTargets")
summary["PrimaryNavigationReasons"] = report.get("PrimaryNavigationReasons",{})
summary["SecondaryNavigationReasons"] = report.get("SecondaryNavigationReasons",{})
for key in ("Presence", "Characters", "Floors", "HouseRouteFailures", "StairTrips", "Workspace", "StartedUtc", "SteadyStartedUtc", "Workload", "RandomMutationSeed", "RandomMutations", "ArtworkAttempts", "ArtworkCreated", "ArtworkClears", "DurableReloadVerification", "ApproachTimeouts", "CharacterApproachTimeouts", "CharacterApproachTimeoutDetails", "NavigationFailureReasons", "NavigationRecoveryAttempts", "ExpectedGeometryChangeRecoveryAttempts", "UnexpectedNavigationRecoveryAttempts", "NavigationRecoveriesCompleted", "NavigationRecoveryTimeouts", "NavigationRecoveryCompletionReasons", "NavigationRecoveryTimeoutReasons", "NavigationRecoveryCompletionMaxSeconds", "CharacterNavigationReasons", "CharacterNavigationRecoveriesCompleted", "CharacterNavigationRecoveryTimeouts", "CharacterNavigationRecoveryCompletionReasons", "CharacterNavigationRecoveryTimeoutReasons", "CharacterNavigationRecoveryCompletionMaxSeconds"):
    if key in report:
        summary[key] = report[key]
summary["Warnings"] = []
summary["Conclusion"] = "PASS"
if not summary["CompletedRequestedDuration"] or report["Exceptions"] or report["StuckSequences"] or report.get("SecondaryStuckSequences",0) or report.get("InvalidFurnitureInteractions",0) or report.get("HouseRouteFailures",0):
    summary["Conclusion"] = "FAIL"
    summary["Warnings"].append("Incomplete duration, exception, sequence stall or house route failure.")
for metric, threshold in [("PrivateMB",32),("HandleCount",50),("NativeWindows",2)]:
    slope=summary["Metrics"].get(metric,{}).get("PostWarmupSlopePerHour")
    if summary["Metrics"].get(metric,{}).get("TrendSampleAdequate") and slope is not None and slope>threshold:
        summary["Warnings"].append(f"{metric} warm-up-excluded slope exceeds review threshold {threshold}/hour.")
navigation_status, summary["NavigationAssessment"], navigation_warning = navigation_assessment(report)
summary["ExpectedGeometryRecoveriesConfirmed"] = navigation_status == "PASS" and report["NavigationFailures"] > 0
if navigation_warning:
    summary["Warnings"].append(navigation_warning)
if navigation_status == "FAIL":
    summary["Conclusion"] = "FAIL"
approach_status, summary["ApproachAssessment"], approach_warning = approach_assessment(report)
if approach_warning:
    summary["Warnings"].append(approach_warning)
if approach_status == "FAIL":
    summary["Conclusion"] = "FAIL"
required_trends = ["PrivateMB","WorkingSetMB","ManagedBytes","HandleCount","NativeWindows","GdiObjects","UserObjects"]
trend_available = all(summary["Metrics"].get(metric,{}).get("TrendSampleAdequate") for metric in required_trends)
summary["ResourceTrendAssessment"] = "Available: at least 30 valid samples spanning 30 minutes after 20-minute warmup" if trend_available else "Not assessed: insufficient post-warmup resource samples or duration"
if report["RequestedSeconds"]>=7200 and not trend_available:
    summary["Warnings"].append("Full soak lacks adequate post-warmup memory/native resource evidence.")
if report["RequestedSeconds"]>=7200 and any(summary["Metrics"].get(metric,{}).get("InvalidSamples",0) for metric in required_trends):
    summary["Warnings"].append("Full soak contains invalid resource measurements; inspect raw samples.")
durability = report.get("DurableReloadVerification")
if isinstance(durability,dict) and durability.get("Succeeded") is not True:
    summary["Conclusion"]="FAIL";summary["Warnings"].append("Final durable snapshot/reload verification failed.")
if report["RequestedSeconds"]>=7200 and not isinstance(durability,dict):
    summary["Warnings"].append("Full soak lacks final durable snapshot/reload verification.")
if any(isinstance(stats.get("Persistence"),dict) and is_counter(stats["Persistence"].get("Failures")) and stats["Persistence"]["Failures"]>0 for stats in summary["Persistence"].values()):
    summary["Conclusion"]="FAIL";summary["Warnings"].append("Persistence worker reported failed writes.")
if summary["Warnings"] and summary["Conclusion"]!="FAIL": summary["Conclusion"]="REVIEW"
summary["Caution"] = "Review post-warmup medians, GC cycles and resource counts together. Raw navigation failures remain reported; only reason-reconciled grounded geometry recoveries within five seconds are exempt from navigation review. No automatic leak-free verdict."
text = json.dumps(summary, indent=2, ensure_ascii=False)
if args.output:
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(text, encoding="utf-8")
print(text)
