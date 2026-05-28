export default function AISphereDemo({ className = '' }: { className?: string }) {
  return (
    <div className={`relative ${className}`}>
      <div className="absolute inset-0 bg-gradient-radial from-accent-primary/20 via-transparent to-transparent" />
      <div className="absolute inset-0 flex items-center justify-center">
        <div className="w-64 h-64 rounded-full bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
          <span className="text-6xl">🤖</span>
        </div>
      </div>
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
        <div className="w-32 h-32 rounded-full bg-accent-primary/30 blur-[80px]" />
      </div>
    </div>
  );
}
