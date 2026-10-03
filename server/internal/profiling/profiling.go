package profiling

import (
	"context"
	"expvar"
	"net/http"
	"os"
	"runtime"
	"runtime/pprof"
	"runtime/trace"
	"sync"
	"time"

	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promauto"
	"github.com/prometheus/client_golang/prometheus/promhttp"
)

// ============================================================================
// COMPREHENSIVE PROFILING & METRICS
// pprof, Prometheus metrics, custom profilers
// ============================================================================

var (
	// Prometheus metrics
	TickDuration = promauto.NewHistogramVec(prometheus.HistogramOpts{
		Name:    "megame_tick_duration_seconds",
		Help:    "Game tick duration in seconds",
		Buckets: prometheus.DefBuckets,
	}, []string{"result"})

	EntityCount = promauto.NewGauge(prometheus.GaugeOpts{
		Name: "megame_entities_total",
		Help: "Current number of entities",
	})

	PlayerCount = promauto.NewGauge(prometheus.GaugeOpts{
		Name: "megame_players_connected",
		Help: "Current number of connected players",
	})

	SnapshotSize = promauto.NewHistogram(prometheus.HistogramOpts{
		Name:    "megame_snapshot_size_bytes",
		Help:    "Network snapshot size in bytes",
		Buckets: prometheus.ExponentialBuckets(1024, 2, 10),
	})

	NetworkBytesSent = promauto.NewCounter(prometheus.CounterOpts{
		Name: "megame_network_bytes_sent_total",
		Help: "Total bytes sent to clients",
	})

	NetworkBytesReceived = promauto.NewCounter(prometheus.CounterOpts{
		Name: "megame_network_bytes_received_total",
		Help: "Total bytes received from clients",
	})

	GoroutineCount = promauto.NewGauge(prometheus.GaugeOpts{
		Name: "megame_goroutines",
		Help: "Current number of goroutines",
	})

	MemoryAlloc = promauto.NewGauge(prometheus.GaugeOpts{
		Name: "megame_memory_alloc_bytes",
		Help: "Current memory allocation in bytes",
	})

	GCCount = promauto.NewCounter(prometheus.CounterOpts{
		Name: "megame_gc_total",
		Help: "Total number of garbage collections",
	})

	SystemUpdateDuration = promauto.NewHistogramVec(prometheus.HistogramOpts{
		Name:    "megame_system_update_duration_seconds",
		Help:    "System update duration",
		Buckets: prometheus.DefBuckets,
	}, []string{"system"})

	RPCCount = promauto.NewCounterVec(prometheus.CounterOpts{
		Name: "megame_rpc_total",
		Help: "Total RPC calls",
	}, []string{"method", "status"})

	RPCErrorCount = promauto.NewCounterVec(prometheus.CounterOpts{
		Name: "megame_rpc_errors_total",
		Help: "Total RPC errors",
	}, []string{"method", "error"})

	// expvar for /debug/vars
	expvarTickCount    = expvar.NewInt("tick_count")
	expvarEntityCount  = expvar.NewInt("entity_count")
	expvarPlayerCount  = expvar.NewInt("player_count")
	expvarBytesSent    = expvar.NewInt("bytes_sent")
	expvarBytesRecv    = expvar.NewInt("bytes_received")
)

// Profiler manages all profiling facilities
type Profiler struct {
	httpServer   *http.Server
	pprofEnabled bool
	metricsPort  int
	pprofPort    int
	stopCh       chan struct{}
	wg           sync.WaitGroup
}

func NewProfiler(metricsPort, pprofPort int) *Profiler {
	return &Profiler{
		metricsPort:  metricsPort,
		pprofPort:    pprofPort,
		stopCh:       make(chan struct{}),
		pprofEnabled: true,
	}
}

func (p *Profiler) Start() {
	p.wg.Add(1)
	go p.runMetricsServer()

	if p.pprofEnabled {
		p.wg.Add(1)
		go p.runPprofServer()
	}

	// Background metrics collector
	p.wg.Add(1)
	go p.collectRuntimeMetrics()
}

func (p *Profiler) Stop() {
	close(p.stopCh)
	if p.httpServer != nil {
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		p.httpServer.Shutdown(ctx)
	}
	p.wg.Wait()
}

func (p *Profiler) runMetricsServer() {
	defer p.wg.Done()

	mux := http.NewServeMux()
	mux.Handle("/metrics", promhttp.Handler())
	mux.Handle("/debug/vars", expvar.Handler())
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		w.Write([]byte("OK"))
	})

	p.httpServer = &http.Server{
		Addr:              ":" + string(rune(p.metricsPort)),
		Handler:           mux,
		ReadHeaderTimeout: 5 * time.Second,
	}

	p.httpServer.ListenAndServe()
}

func (p *Profiler) runPprofServer() {
	defer p.wg.Done()

	mux := http.NewServeMux()
	mux.HandleFunc("/debug/pprof/", pprof.Index)
	mux.HandleFunc("/debug/pprof/cmdline", pprof.Cmdline)
	mux.HandleFunc("/debug/pprof/profile", pprof.Profile)
	mux.HandleFunc("/debug/pprof/symbol", pprof.Symbol)
	mux.HandleFunc("/debug/pprof/trace", pprof.Trace)
	mux.Handle("/debug/pprof/goroutine", pprof.Handler("goroutine"))
	mux.Handle("/debug/pprof/heap", pprof.Handler("heap"))
	mux.Handle("/debug/pprof/threadcreate", pprof.Handler("threadcreate"))
	mux.Handle("/debug/pprof/block", pprof.Handler("block"))
	mux.Handle("/debug/pprof/mutex", pprof.Handler("mutex"))

	server := &http.Server{
		Addr:              ":" + string(rune(p.pprofPort)),
		Handler:           mux,
		ReadHeaderTimeout: 5 * time.Second,
	}

	server.ListenAndServe()
}

func (p *Profiler) collectRuntimeMetrics() {
	defer p.wg.Done()

	ticker := time.NewTicker(10 * time.Second)
	defer ticker.Stop()

	var memStats runtime.MemStats
	var lastGC uint32

	for {
		select {
		case <-p.stopCh:
			return
		case <-ticker.C:
			runtime.ReadMemStats(&memStats)

			// Update Prometheus
			GoroutineCount.Set(float64(runtime.NumGoroutine()))
			MemoryAlloc.Set(float64(memStats.Alloc))

			// Update expvar
			expvarEntityCount.Set(int64(EntityCount))
			expvarPlayerCount.Set(int64(PlayerCount))
			expvarBytesSent.Set(int64(NetworkBytesSent))
			expvarBytesRecv.Set(int64(NetworkBytesReceived))

			// GC count
			gcDelta := memStats.NumGC - lastGC
			if gcDelta > 0 {
				GCCount.Add(float64(gcDelta))
				lastGC = memStats.NumGC
			}
		}
	}
}

// ============================================================================
// Custom Profiling Helpers
// ============================================================================

// ProfileFunc profiles a function execution
func ProfileFunc(name string, fn func()) {
	start := time.Now()
	fn()
	duration := time.Since(start)

	SystemUpdateDuration.WithLabelValues(name).Observe(duration.Seconds())
}

// ProfileFuncWithResult profiles a function with return value
func ProfileFuncWithResult[T any](name string, fn func() T) T {
	start := time.Now()
	result := fn()
	duration := time.Since(start)

	SystemUpdateDuration.WithLabelValues(name).Observe(duration.Seconds())
	return result
}

// RecordRPC records RPC metrics
func RecordRPC(method string, success bool, err error) {
	status := "success"
	if !success {
		status = "error"
	}
	RPCCount.WithLabelValues(method, status).Inc()

	if err != nil {
		RPCErrorCount.WithLabelValues(method, err.Error()).Inc()
	}
}

// RecordSnapshot records snapshot metrics
func RecordSnapshot(size int, entityCount int, playerCount int) {
	SnapshotSize.Observe(float64(size))
	EntityCount.Set(float64(entityCount))
	PlayerCount.Set(float64(playerCount))
}

// RecordNetwork records network I/O
func RecordNetwork(sent, received int) {
	NetworkBytesSent.Add(float64(sent))
	NetworkBytesReceived.Add(float64(received))
	expvarBytesSent.Add(int64(sent))
	expvarBytesRecv.Add(int64(received))
}

// ============================================================================
// CPU Profile Helper
// ============================================================================

type CPUProfile struct {
	file *os.File
}

func StartCPUProfile(path string) (*CPUProfile, error) {
	f, err := os.Create(path)
	if err != nil {
		return nil, err
	}
	if err := pprof.StartCPUProfile(f); err != nil {
		f.Close()
		return nil, err
	}
	return &CPUProfile{file: f}, nil
}

func (cp *CPUProfile) Stop() {
	pprof.StopCPUProfile()
	if cp.file != nil {
		cp.file.Close()
	}
}

// ============================================================================
// Memory Profile Helper
// ============================================================================

func WriteMemoryProfile(path string) error {
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()

	runtime.GC()
	return pprof.WriteHeapProfile(f)
}

// ============================================================================
// Trace Helper
// ============================================================================

type Trace struct {
	file *os.File
}

func StartTrace(path string) (*Trace, error) {
	f, err := os.Create(path)
	if err != nil {
		return nil, err
	}
	if err := trace.Start(f); err != nil {
		f.Close()
		return nil, err
	}
	return &Trace{file: f}, nil
}

func (t *Trace) Stop() {
	trace.Stop()
	if t.file != nil {
		t.file.Close()
	}
}

// ============================================================================
// Block/Mutex Profile Helpers
// ============================================================================

func SetBlockProfileRate(rate int) {
	runtime.SetBlockProfileRate(rate)
}

func SetMutexProfileFraction(rate int) {
	runtime.SetMutexProfileFraction(rate)
}

func WriteBlockProfile(path string) error {
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return pprof.Lookup("block").WriteTo(f, 0)
}

func WriteMutexProfile(path string) error {
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return pprof.Lookup("mutex").WriteTo(f, 0)
}

// ============================================================================
// System Timer (for internal system profiling)
// ============================================================================

type SystemTimer struct {
	name   string
	start  time.Time
	labels map[string]string
}

func NewSystemTimer(name string) *SystemTimer {
	return &SystemTimer{
		name:  name,
		start: time.Now(),
	}
}

func (t *SystemTimer) Stop() {
	duration := time.Since(t.start)
	SystemUpdateDuration.WithLabelValues(t.name).Observe(duration.Seconds())
}

// ============================================================================
// Integration with Game Server
// ============================================================================

type ServerMetrics struct {
	profiler *Profiler
}

func NewServerMetrics(metricsPort, pprofPort int) *ServerMetrics {
	profiler := NewProfiler(metricsPort, pprofPort)
	profiler.Start()
	return &ServerMetrics{profiler: profiler}
}

func (m *ServerMetrics) RecordTick(duration time.Duration, entities, players int) {
	TickDuration.WithLabelValues("success").Observe(duration.Seconds())
	expvarTickCount.Add(1)
	RecordSnapshot(0, entities, players)
}

func (m *ServerMetrics) RecordNetwork(sent, received int) {
	RecordNetwork(sent, received)
}

func (m *ServerMetrics) RecordRPC(method string, success bool, err error) {
	RecordRPC(method, success, err)
}

func (m *ServerMetrics) Stop() {
	m.profiler.Stop()
}