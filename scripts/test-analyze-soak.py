"""Regression coverage for the installation gate's navigation classification."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class NavigationGateTests(unittest.TestCase):
    def analyze(self, failures=0, rejected=0, duration=600, telemetry=None):
        report = dict(Seconds=duration, RequestedSeconds=600, Exceptions=0,
                      StuckSequences=0, NavigationFailures=failures, PathPlans=4,
                      MachineCpuPercent=1, AllocatedMB=10, PhaseFrames={},
                      UnreachableTargets=rejected,
                      PrimaryNavigationReasons={"unexpected-landing": failures} if failures else {},
                      Samples=[dict(Second=duration, WallSeconds=duration,
                                    CpuSeconds=1, PrivateMB=100)])
        report.update(telemetry or {})
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "report.json"
            path.write_text(json.dumps(report), encoding="utf-8")
            result = subprocess.run([sys.executable, str(Path(__file__).with_name("analyze-soak.py")), str(path)],
                                    capture_output=True, text=True, check=True)
            return json.loads(result.stdout)

    def geometry_telemetry(self, counts=None):
        counts = counts or {"Cat": 1, "Girl": 2, "BorderCollie": 0}
        total = sum(counts.values())
        reason = "geometry-changed-in-flight"
        character_reasons = {kind: {reason: count} if count else {} for kind, count in counts.items()}
        secondary = sum(count for kind, count in counts.items() if kind != "Cat")
        return dict(Characters=list(counts), NavigationRecoveryAttempts=total,
                    ExpectedGeometryChangeRecoveryAttempts=total, UnexpectedNavigationRecoveryAttempts=0,
                    NavigationRecoveriesCompleted=total, NavigationRecoveryTimeouts=0,
                    NavigationFailureReasons={reason: total}, NavigationRecoveryCompletionReasons={reason: total},
                    NavigationRecoveryTimeoutReasons={}, NavigationRecoveryCompletionMaxSeconds=1.5,
                    PrimaryNavigationReasons=character_reasons["Cat"],
                    SecondaryNavigationReasons={reason: secondary} if secondary else {},
                    CharacterNavigationReasons=character_reasons,
                    CharacterNavigationRecoveriesCompleted=dict(counts),
                    CharacterNavigationRecoveryTimeouts={kind: 0 for kind in counts},
                    CharacterNavigationRecoveryCompletionReasons={kind: dict(reasons) for kind, reasons in character_reasons.items()},
                    CharacterNavigationRecoveryTimeoutReasons={kind: {} for kind in counts},
                    CharacterNavigationRecoveryCompletionMaxSeconds={kind: 1.5 if count else 0 for kind, count in counts.items()},
                    CharacterNavigationRecoveryDetails={kind: [dict(Reason=reason, ElapsedSeconds=1.5, Grounded=True, ReachedEscape=True, TimedOut=False)] if count else [] for kind, count in counts.items()})

    def test_real_navigation_failure_still_blocks_installation(self):
        result = self.analyze(failures=1)
        self.assertEqual("REVIEW", result["Conclusion"])
        self.assertEqual(1, result["PrimaryNavigationReasons"]["unexpected-landing"])

    def test_rejected_target_is_not_an_executed_route_failure(self):
        result = self.analyze(rejected=3)
        self.assertEqual("PASS", result["Conclusion"])
        self.assertEqual(3, result["UnreachableTargets"])
        self.assertIn("Not assessed", result["ResourceTrendAssessment"])

    def test_rejections_cannot_hide_real_failure(self):
        self.assertEqual("REVIEW", self.analyze(failures=2, rejected=20)["Conclusion"])

    def approach_telemetry(self):
        characters = ["Cat", "Girl", "BorderCollie"]
        return dict(Floors=3, Characters=characters, ApproachTimeouts=0,
                    CharacterApproachTimeouts={kind: 0 for kind in characters},
                    CharacterApproachTimeoutDetails={kind: [] for kind in characters},
                    Samples=[dict(Second=600, WallSeconds=600, CpuSeconds=1,
                                  PrivateMB=100, ApproachTimeouts=0)])

    def test_current_approach_counters_pass_when_complete_and_zero(self):
        result = self.analyze(telemetry=self.approach_telemetry())
        self.assertEqual("PASS", result["Conclusion"])
        self.assertEqual("No approach timeouts", result["ApproachAssessment"])

    def test_approach_fallback_cannot_hide_timeout(self):
        telemetry = self.approach_telemetry()
        telemetry["ApproachTimeouts"] = 1
        telemetry["CharacterApproachTimeouts"]["Girl"] = 1
        self.assertEqual("FAIL", self.analyze(telemetry=telemetry)["Conclusion"])

    def test_house_route_failure_blocks_analysis(self):
        telemetry = self.approach_telemetry()
        telemetry["HouseRouteFailures"] = 1
        self.assertEqual("FAIL", self.analyze(telemetry=telemetry)["Conclusion"])

    def test_character_sample_and_retained_timeout_cannot_hide_behind_aggregate(self):
        for mutate in (lambda t: t["CharacterApproachTimeouts"].update(BorderCollie=1),
                       lambda t: t["Samples"][0].update(ApproachTimeouts=1),
                       lambda t: t["CharacterApproachTimeoutDetails"]["Cat"].append(dict(Kind="Cat"))):
            with self.subTest(mutate=mutate):
                telemetry = self.approach_telemetry()
                mutate(telemetry)
                self.assertEqual("FAIL", self.analyze(telemetry=telemetry)["Conclusion"])

    def test_missing_or_malformed_current_approach_telemetry_needs_review(self):
        for mutate in (lambda t: t.pop("ApproachTimeouts"),
                       lambda t: t.update(ApproachTimeouts=False),
                       lambda t: t["CharacterApproachTimeouts"].pop("Girl"),
                       lambda t: t["Samples"][0].pop("ApproachTimeouts"),
                       lambda t: t["CharacterApproachTimeoutDetails"].update(Cat="unknown")):
            with self.subTest(mutate=mutate):
                telemetry = self.approach_telemetry()
                mutate(telemetry)
                self.assertEqual("REVIEW", self.analyze(telemetry=telemetry)["Conclusion"])

    def test_legacy_baseline_keeps_its_telemetry_boundary(self):
        result = self.analyze()
        self.assertEqual("PASS", result["Conclusion"])
        self.assertIn("Not assessed: legacy", result["ApproachAssessment"])

    def test_incomplete_run_is_failure(self):
        self.assertEqual("FAIL", self.analyze(duration=450)["Conclusion"])

    def test_expected_grounded_bounded_geometry_recoveries_preserve_raw_failures(self):
        result = self.analyze(failures=3, telemetry=self.geometry_telemetry())
        self.assertEqual("PASS", result["Conclusion"])
        self.assertEqual(3, result["NavigationFailures"])
        self.assertEqual(3, result["NavigationRecoveriesCompleted"])
        self.assertTrue(result["ExpectedGeometryRecoveriesConfirmed"])

    def test_capped_details_do_not_supply_all_time_totals(self):
        telemetry = self.geometry_telemetry({"Cat": 100, "Girl": 0, "BorderCollie": 0})
        telemetry["CharacterNavigationRecoveryDetails"]["Cat"] *= 32
        result = self.analyze(failures=100, telemetry=telemetry)
        self.assertEqual("PASS", result["Conclusion"])
        self.assertEqual(100, result["NavigationFailures"])

    def test_successful_unexpected_recovery_is_still_review(self):
        telemetry = self.geometry_telemetry()
        telemetry["UnexpectedNavigationRecoveryAttempts"] = 1
        telemetry["ExpectedGeometryChangeRecoveryAttempts"] = 2
        self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_aggregate_counts_cannot_hide_wrong_character_completions(self):
        telemetry = self.geometry_telemetry()
        telemetry["CharacterNavigationRecoveriesCompleted"].update(Cat=0, Girl=3)
        self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_character_unexpected_reason_cannot_hide_behind_global_geometry(self):
        telemetry = self.geometry_telemetry()
        telemetry["CharacterNavigationReasons"]["Girl"] = {"no-progress": 2}
        self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_missing_or_mismatched_bound_does_not_confirm_geometry_recovery(self):
        for mutate in (lambda t: t.pop("NavigationRecoveryCompletionMaxSeconds"),
                       lambda t: t.update(NavigationRecoveryCompletionMaxSeconds=5.01),
                       lambda t: t["CharacterNavigationRecoveryCompletionMaxSeconds"].update(Girl=4)):
            with self.subTest(mutate=mutate):
                telemetry = self.geometry_telemetry()
                mutate(telemetry)
                self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_unreached_or_ungrounded_detail_does_not_confirm_completion(self):
        for field in ("Grounded", "ReachedEscape"):
            with self.subTest(field=field):
                telemetry = self.geometry_telemetry()
                telemetry["CharacterNavigationRecoveryDetails"]["Cat"][0][field] = False
                self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_timeout_fails_even_if_later_completions_equal_attempts(self):
        telemetry = self.geometry_telemetry()
        telemetry["NavigationRecoveryTimeouts"] = 1
        result = self.analyze(failures=3, telemetry=telemetry)
        self.assertEqual("FAIL", result["Conclusion"])

    def test_character_timeout_cannot_be_hidden_by_zero_aggregate(self):
        telemetry = self.geometry_telemetry()
        telemetry["CharacterNavigationRecoveryTimeouts"]["Girl"] = 1
        self.assertEqual("FAIL", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_timeout_detail_fails_even_if_counters_are_inconsistent(self):
        telemetry = self.geometry_telemetry()
        telemetry["CharacterNavigationRecoveryDetails"]["Cat"][0]["TimedOut"] = True
        self.assertEqual("FAIL", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_boolean_reason_count_does_not_act_as_integer_evidence(self):
        telemetry = self.geometry_telemetry()
        telemetry["CharacterNavigationRecoveryCompletionReasons"]["Cat"] = {"geometry-changed-in-flight": True}
        self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def test_incomplete_duration_fails_even_with_verified_geometry_recovery(self):
        self.assertEqual("FAIL", self.analyze(failures=3, duration=450, telemetry=self.geometry_telemetry())["Conclusion"])

    def test_malformed_retained_details_do_not_confirm_geometry_recovery(self):
        telemetry = self.geometry_telemetry()
        telemetry["CharacterNavigationRecoveryDetails"]["Girl"] = "unknown"
        self.assertEqual("REVIEW", self.analyze(failures=3, telemetry=telemetry)["Conclusion"])

    def resource_telemetry(self, growth=0):
        rows=[]
        for second in range(30,7201,30):
            rows.append(dict(Second=second,WallSeconds=second,CpuSeconds=second*.2,
                             WorkingSetMB=250,PrivateMB=150+growth*max(0,second-1200)/3600,
                             ManagedBytes=14000000,HandleCount=1600,GdiObjects=377,
                             UserObjects=180,NativeWindows=83,Windows=32,ArtWindows=1,
                             ArtworkCount=(second//67)%32,DecodedCompanionAtlases=3))
        return dict(RequestedSeconds=7200,Samples=rows,
                    DurableReloadVerification=dict(Succeeded=True,CharacterKinds=["Cat","Girl","BorderCollie"]))

    def test_full_resource_trends_require_actual_post_warmup_coverage(self):
        telemetry=self.resource_telemetry()
        result=self.analyze(duration=7200,telemetry=telemetry)
        self.assertEqual("PASS",result["Conclusion"])
        self.assertTrue(result["Metrics"]["PrivateMB"]["TrendSampleAdequate"])
        self.assertEqual(0,result["Metrics"]["PrivateMB"]["PostWarmupSlopePerHour"])
        self.assertIn("Available",result["ResourceTrendAssessment"])
        telemetry["Samples"]=[telemetry["Samples"][-1]]
        result=self.analyze(duration=7200,telemetry=telemetry)
        self.assertEqual("REVIEW",result["Conclusion"])
        self.assertIn("Not assessed",result["ResourceTrendAssessment"])

    def test_private_memory_growth_requires_review_even_with_no_navigation_error(self):
        result=self.analyze(duration=7200,telemetry=self.resource_telemetry(growth=40))
        self.assertEqual("REVIEW",result["Conclusion"])
        self.assertAlmostEqual(40,result["Metrics"]["PrivateMB"]["PostWarmupSlopePerHour"])

    def test_sparse_short_trend_does_not_pretend_to_assess_leaks(self):
        telemetry=dict(Samples=[dict(Second=t,WallSeconds=t,CpuSeconds=t*.2,PrivateMB=t) for t in (1200,1230,1260)])
        result=self.analyze(duration=1260,telemetry=telemetry)
        self.assertEqual("PASS",result["Conclusion"])
        self.assertFalse(result["Metrics"]["PrivateMB"]["TrendSampleAdequate"])
        self.assertIn("Not assessed",result["ResourceTrendAssessment"])

    def test_invalid_resource_readings_cannot_pass_full_evidence(self):
        telemetry=self.resource_telemetry();telemetry["Samples"][45]["PrivateMB"]=True
        result=self.analyze(duration=7200,telemetry=telemetry)
        self.assertEqual("REVIEW",result["Conclusion"])
        self.assertEqual(1,result["Metrics"]["PrivateMB"]["InvalidSamples"])

    def test_persistence_failure_and_failed_durable_reload_fail_gate(self):
        for telemetry in (dict(Performance=dict(Persistence=dict(Failures=1))),
                          dict(DurableReloadVerification=dict(Succeeded=False))):
            with self.subTest(telemetry=telemetry):
                self.assertEqual("FAIL",self.analyze(telemetry=telemetry)["Conclusion"])

    def test_ui_capture_barrier_and_worker_are_reported_separately(self):
        performance=dict(DispatcherSamples=100,DispatcherDelayP50UpperMs=10,DispatcherDelayP95UpperMs=20,
                         SnapshotCaptureCount=5,SnapshotCaptureP95UpperMs=3,SaveCount=0,
                         Persistence=dict(Failures=0,WriteMeanMs=90,DurableMeanMs=95),
                         PersistenceObservation=dict(Written=5,WriteMeanMs=80,LifetimeWriteMaxMs=150))
        result=self.analyze(telemetry=dict(Performance=performance))
        self.assertEqual(3,result["UiLatency"]["Steady"]["SnapshotCaptureP95UpperMs"])
        self.assertEqual(90,result["Persistence"]["Workload"]["Persistence"]["WriteMeanMs"])
        self.assertEqual(150,result["Persistence"]["Workload"]["PersistenceObservation"]["LifetimeWriteMaxMs"])
        self.assertEqual("PASS",result["Conclusion"])


if __name__ == "__main__":
    unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout, verbosity=1))
