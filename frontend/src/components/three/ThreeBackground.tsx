export default function ThreeBackground() {
  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none">
      {/* Gradient Background */}
      <div className="absolute inset-0 bg-gradient-to-b from-bg-primary via-bg-secondary to-bg-primary" />
      
      {/* Animated gradient orbs */}
      <div className="absolute top-1/4 left-1/4 w-[500px] h-[500px] bg-accent-primary/20 rounded-full blur-[120px] animate-pulse" />
      <div className="absolute top-1/3 right-1/4 w-[400px] h-[400px] bg-violet/20 rounded-full blur-[100px] animate-pulse" style={{ animationDelay: '1s' }} />
      <div className="absolute bottom-1/4 left-1/2 w-[600px] h-[600px] bg-accent-secondary/10 rounded-full blur-[150px] animate-pulse" style={{ animationDelay: '2s' }} />
      
      {/* Grid pattern */}
      <div 
        className="absolute inset-0 opacity-5"
        style={{
          backgroundImage: `linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px),
                           linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)`,
          backgroundSize: '50px 50px'
        }}
      />
    </div>
  );
}
