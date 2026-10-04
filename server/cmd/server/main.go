package main

import (
	"context"
	"flag"
	"fmt"
	"net"
	"os"
	"os/signal"
	"path/filepath"
	"syscall"
	"time"

	"github.com/megame/server/internal/components"
	"github.com/megame/server/internal/ecs"
	"github.com/megame/server/internal/network"
	"github.com/megame/server/internal/persistence"
	"github.com/megame/server/internal/systems"
	"go.uber.org/zap"
	"google.golang.org/grpc"
)

var (
	port      = flag.Int("port", 50051, "gRPC server port")
	httpPort  = flag.Int("http-port", 8080, "HTTP metrics port")
	saveDir   = flag.String("save-dir", "./saves", "Save directory")
	tickRate  = flag.Int("tick-rate", 60, "Server tick rate")
	logLevel  = flag.String("log-level", "info", "Log level (debug, info, warn, error)")
	healthChk = flag.Bool("health-check", false, "Run a self health check and exit")
)

func main() {
	flag.Parse()

	// The container HEALTHCHECK invokes the binary with -health-check. Exit
	// before any server setup so the probe stays cheap.
	if *healthChk {
		if err := healthCheck(*saveDir); err != nil {
			fmt.Fprintf(os.Stderr, "health check failed: %v\n", err)
			os.Exit(1)
		}
		fmt.Println("ok")
		return
	}

	// Initialize logger
	logger, err := newLogger(*logLevel)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Failed to create logger: %v\n", err)
		os.Exit(1)
	}
	defer logger.Sync()

	logger.Info("Starting MegaGame Server",
		zap.Int("port", *port),
		zap.Int("tick_rate", *tickRate),
		zap.String("save_dir", *saveDir),
	)

	// Create ECS world
	world := ecs.NewWorld()

	// Register all components
	components.RegisterAllComponents(world.Registry())

	// Create system manager
	systemMgr := ecs.NewSystemManager()

	// Add systems in priority order
	systemMgr.Add(systems.NewInputSystem())
	systemMgr.Add(systems.NewMovementSystem())
	systemMgr.Add(systems.NewVehicleSystem())
	systemMgr.Add(systems.NewWeaponSwitchSystem())
	systemMgr.Add(systems.NewWeaponSystem())
	systemMgr.Add(systems.NewMissionSystem())
	systemMgr.Add(systems.NewDialogueSystem())
	systemMgr.Add(systems.NewAISystem())

	// Create game server
	gameServer := network.NewGameServer(world, systemMgr)

	// Create persistence manager
	saveManager := persistence.NewSaveManager(*saveDir, world)
	autoSave := persistence.NewAutoSave(saveManager, 5*time.Minute, "autosave")
	autoSave.Start()
	defer autoSave.Stop()

	// Create gRPC server
	grpcServer := grpc.NewServer(
		grpc.MaxRecvMsgSize(1024*1024*10), // 10MB
		grpc.MaxSendMsgSize(1024*1024*10),
	)

	network.RegisterGRPC(grpcServer, gameServer)

	// Start gRPC server
	lis, err := net.Listen("tcp", fmt.Sprintf(":%d", *port))
	if err != nil {
		logger.Fatal("Failed to listen", zap.Error(err))
	}

	// Start game loop
	gameServer.Start()

	// Handle shutdown
	_, cancel := context.WithCancel(context.Background())
	defer cancel()

	go func() {
		sigCh := make(chan os.Signal, 1)
		signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)
		<-sigCh

		logger.Info("Shutdown signal received")
		gameServer.Stop()
		grpcServer.GracefulStop()
		cancel()
	}()

	logger.Info("Server listening", zap.String("address", lis.Addr().String()))

	if err := grpcServer.Serve(lis); err != nil {
		logger.Fatal("gRPC server failed", zap.Error(err))
	}

	logger.Info("Server stopped")
}

func newLogger(level string) (*zap.Logger, error) {
	var cfg zap.Config
	if level == "debug" {
		cfg = zap.NewDevelopmentConfig()
	} else {
		cfg = zap.NewProductionConfig()
	}

	// Set log level
	switch level {
	case "debug":
		cfg.Level = zap.NewAtomicLevelAt(zap.DebugLevel)
	case "info":
		cfg.Level = zap.NewAtomicLevelAt(zap.InfoLevel)
	case "warn":
		cfg.Level = zap.NewAtomicLevelAt(zap.WarnLevel)
	case "error":
		cfg.Level = zap.NewAtomicLevelAt(zap.ErrorLevel)
	}

	return cfg.Build()
}

// healthCheck verifies the process can still do its job: it must be able to
// create and write to the save directory. Run by the container HEALTHCHECK.
func healthCheck(dir string) error {
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return fmt.Errorf("save dir %q is not usable: %w", dir, err)
	}
	probe := filepath.Join(dir, ".healthcheck")
	if err := os.WriteFile(probe, []byte("ok"), 0o644); err != nil {
		return fmt.Errorf("save dir %q is not writable: %w", dir, err)
	}
	if err := os.Remove(probe); err != nil {
		return fmt.Errorf("save dir %q is not writable: %w", dir, err)
	}
	return nil
}
