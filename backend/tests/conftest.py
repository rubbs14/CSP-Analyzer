import threadpoolctl

# The golden-baseline regression tests pin exact floating-point output from a
# fitted scikit-learn pipeline. Multi-threaded BLAS reduction order isn't
# guaranteed bit-identical across thread counts, so pin to a single thread
# for the whole test session to keep those comparisons reproducible.
threadpoolctl.threadpool_limits(limits=1)
