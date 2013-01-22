require 'albacore'

task :default => [:build]

msbuild :build do |msb|
  msb.solution = "BuyLottery.sln"
  msb.targets :clean, :build
  msb.properties :configuration => :release
end

